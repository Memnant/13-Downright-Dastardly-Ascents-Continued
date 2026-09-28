using System;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using Peak;

namespace dda;

internal static class RunSaveStore
{
    [DataContract] private sealed class NativeFile
    {
        [DataMember] public NativeRun run = null;
    }
    [DataContract] private sealed class NativeRun
    {
        [DataMember] public string runId = null;
        [DataMember] public int biomeReached = 0;
        [DataMember] public float runTimer = 0;
    }
    private static string Location(Guid id) => Path.Combine(Paths.ConfigPath, "13dda-continued", "runs", id.ToString("N") + ".json");
    private static string Hash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
    }

    // Native SaveNow has returned successfully. This runs on every client, not only the host.
    internal static void SaveCheckpoint()
    {
        if (!RunCoordinator.InRun || RunCoordinator.ResumePrepared) return;
        byte[] nativeBytes = File.ReadAllBytes(Quicksave.SaveFile.FullName);
        var native = WireJson.Decode<NativeFile>(Encoding.UTF8.GetString(nativeBytes));
        if (native?.run == null || !Guid.TryParse(native.run.runId, out Guid id) || id == Guid.Empty || id != TideSync.RunId)
            throw new InvalidDataException("Native checkpoint does not match the current run.");
        double? phase = null;
        if (Rules.Enabled(14))
        {
            if (!TideSync.TryPhase(id, out double value)) throw new InvalidDataException("Waiting for the host's lava phase; metadata not overwritten.");
            phase = value;
        }
        var record = new SavedRuleRecord {
            schema = SavedRuleRecord.CurrentSchema, runId = id.ToString("N"), gameVersion = Plugin.SupportedGame,
            modVersion = Plugin.Version, rules = RunCoordinator.Active.Encode(), nativeSaveHash = Hash(nativeBytes),
            checkpointSegment = native.run.biomeReached, checkpointTime = native.run.runTimer, tidePhase = phase
        };
        if (!record.TryRead(id, Plugin.SupportedGame, Plugin.Version, out _)) throw new InvalidDataException("Invalid checkpoint metadata.");
        string path = Location(id);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, record.ToJson());
        if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
        else File.Move(temporary, path);
        Plugin.Log.LogInfo($"Saved Continued checkpoint: {id:N}, segment={record.checkpointSegment}, tide={phase?.ToString("R") ?? "off"}.");
    }

    internal static bool PrepareResume(string runId, int officialAscent)
    {
        try
        {
            if (!Guid.TryParse(runId, out Guid id) || id == Guid.Empty) throw new InvalidDataException("Invalid native run ID.");
            if (officialAscent < -1 || officialAscent > 8) throw new InvalidDataException("Legacy out-of-range ascents cannot be automatically migrated.");
            string path = Location(id);
            var snapshot = DifficultySnapshot.Official;
            double phase = 0;
            if (File.Exists(path))
            {
                var record = SavedRuleRecord.FromJson(File.ReadAllText(path));
                if (record == null || !record.TryRead(id, Plugin.SupportedGame, Plugin.Version, out snapshot))
                    throw new InvalidDataException("Incompatible or damaged Continued run metadata.");
                if (snapshot.IsExtended && officialAscent != 8) throw new InvalidDataException("Extended metadata requires official Ascent 8 as its base.");
                if (record.schema == SavedRuleRecord.CurrentSchema &&
                    !string.Equals(record.nativeSaveHash, Hash(File.ReadAllBytes(Quicksave.SaveFile.FullName)), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Continued metadata belongs to a different checkpoint; native save was preserved.");
                phase = record.tidePhase ?? 0;
                if (record.schema == 1) Plugin.Log.LogInfo("Migrated 1.5.0 rules in memory; missing lava phase starts at zero for everyone. Original metadata remains until the next checkpoint.");
            }
            RunCoordinator.Begin(snapshot, fresh: true);
            RunCoordinator.ResumePrepared = true;
            OpeningProtection.DisableForResume();
            WeatherGrace.Arm(id);
            SummitHonor.ClearRoom();
            TideSync.PrepareResume(id, phase);
            RunCoordinator.Publish(true);
            Plugin.Log.LogInfo("Restored run rules: " + snapshot.Encode());
            return true;
        }
        catch (Exception error)
        {
            RunCoordinator.Notice = "续作存档信息无法读取或不匹配当前营火；已取消加载，原存档保留。";
            Plugin.Log.LogError(RunCoordinator.Notice + " " + error.Message);
            return false;
        }
    }
}
