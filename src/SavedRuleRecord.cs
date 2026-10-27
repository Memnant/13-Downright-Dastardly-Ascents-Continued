using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace dda;

[Serializable]
[DataContract]
public sealed class SavedRuleRecord
{
    public const int CurrentSchema = 2;
    // No defaults: missing JSON fields must not silently validate as a current record.
    [DataMember] public int schema;
    [DataMember] public string runId;
    [DataMember] public string gameVersion;
    [DataMember] public string modVersion;
    [DataMember] public string rules;
    [DataMember] public string nativeSaveHash;
    [DataMember] public int checkpointSegment;
    [DataMember] public double checkpointTime;
    [DataMember] public double? tidePhase;
    [DataMember(EmitDefaultValue = false)] public int? nativeSaveVersion;

    public string ToJson()
    {
        using var stream = new MemoryStream();
        new DataContractJsonSerializer(typeof(SavedRuleRecord)).WriteObject(stream, this);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    public static SavedRuleRecord FromJson(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return (SavedRuleRecord)new DataContractJsonSerializer(typeof(SavedRuleRecord)).ReadObject(stream);
    }

    public bool TryRead(Guid expectedRun, string expectedGame, string expectedMod, out DifficultySnapshot snapshot)
    {
        snapshot = DifficultySnapshot.Official;
        // 2.5 keeps the native RunId/checkpoint format. Migrate known 2.4.c
        // sidecars in memory only; RunSaveStore still verifies the exact native
        // checkpoint hash before accepting them. No game-version downgrade.
        // 2.6 uses format 4: never extend the 2.4/2.5 migration into 2.6.
        // The relaxed reader separately permits same-format forward 2.6 patches.
        // 1.5.22 and later (PEAK 3.x, format 4) read any sidecar written by 1.5.22 up to their own
        // version. No per-release literal: a new mod version keeps reading older 3.x records,
        // while an older mod still rejects a newer record. 2.6/1.5.21 records stay unmigrated.
        bool currentReader = SameOrNewer(expectedMod, FirstThreeXWriter);
        if (currentReader && (nativeSaveVersion != GameVersionPolicy.NativeSaveVersion ||
            !SameOrNewer(modVersion, FirstThreeXWriter) || !SameOrNewer(expectedMod, modVersion) ||
            !GameVersionPolicy.CanReadSave(gameVersion, expectedGame))) return false;
        bool relaxedReader = expectedMod == "1.5.21";
        if (relaxedReader && !(nativeSaveVersion == GameVersionPolicy.NativeSaveVersion ||
            (nativeSaveVersion == null && modVersion == "1.5.20" && gameVersion == "2.6.a"))) return false;
        bool compatibleGame = currentReader ? true : relaxedReader ? CanReadLegacy26(gameVersion, expectedGame) :
            ((!string.IsNullOrEmpty(gameVersion) && gameVersion == expectedGame) ||
            (gameVersion == "2.4.c" && expectedGame == "2.5.a" && (expectedMod == "1.5.17" || expectedMod == "1.5.18" || expectedMod == "1.5.19") && KnownPriorVersion(modVersion, 0)));
        if (expectedRun == Guid.Empty || runId != expectedRun.ToString("N") || !compatibleGame ||
            !DifficultySnapshot.TryDecode(rules, out snapshot)) return false;
        if (schema == 1) return modVersion == "1.5.0" &&
            (expectedMod == "1.5.0" || expectedMod == "1.5.1" || expectedMod == "1.5.2" || expectedMod == "1.5.3" || expectedMod == "1.5.4" || expectedMod == "1.5.5" || expectedMod == "1.5.6" || expectedMod == "1.5.7" || expectedMod == "1.5.8" || expectedMod == "1.5.9" || expectedMod == "1.5.10" || expectedMod == "1.5.11" || expectedMod == "1.5.12" || expectedMod == "1.5.13" || expectedMod == "1.5.14" || expectedMod == "1.5.15" || expectedMod == "1.5.16" || expectedMod == "1.5.17" || expectedMod == "1.5.18" || expectedMod == "1.5.19");
        bool compatibleVersion = modVersion == expectedMod || currentReader ||
            (expectedMod == "1.5.21" && modVersion == "1.5.20" && gameVersion == "2.6.a") ||
            (expectedMod == "1.5.19" && KnownPriorVersion(modVersion, 1, 18)) ||
            (expectedMod == "1.5.18" && KnownPriorVersion(modVersion, 1, 17)) ||
            (expectedMod == "1.5.17" && KnownPriorVersion(modVersion, 1)) ||
            (modVersion == "1.5.1" && expectedMod == "1.5.2") ||
            (expectedMod == "1.5.3" && (modVersion == "1.5.1" || modVersion == "1.5.2")) ||
            (expectedMod == "1.5.4" && (modVersion == "1.5.1" || modVersion == "1.5.2" || modVersion == "1.5.3")) ||
            (expectedMod == "1.5.5" && (modVersion == "1.5.1" || modVersion == "1.5.2" || modVersion == "1.5.3" || modVersion == "1.5.4")) ||
            (expectedMod == "1.5.6" && (modVersion == "1.5.1" || modVersion == "1.5.2" || modVersion == "1.5.3" || modVersion == "1.5.4" || modVersion == "1.5.5")) ||
            (expectedMod == "1.5.7" && (modVersion == "1.5.1" || modVersion == "1.5.2" || modVersion == "1.5.3" || modVersion == "1.5.4" || modVersion == "1.5.5" || modVersion == "1.5.6")) ||
            (expectedMod == "1.5.8" && (modVersion == "1.5.1" || modVersion == "1.5.2" || modVersion == "1.5.3" || modVersion == "1.5.4" || modVersion == "1.5.5" || modVersion == "1.5.6" || modVersion == "1.5.7")) ||
            (expectedMod == "1.5.9" && (modVersion == "1.5.1" || modVersion == "1.5.2" || modVersion == "1.5.3" || modVersion == "1.5.4" || modVersion == "1.5.5" || modVersion == "1.5.6" || modVersion == "1.5.7" || modVersion == "1.5.8")) ||
            (expectedMod == "1.5.10" && (modVersion == "1.5.1" || modVersion == "1.5.2" || modVersion == "1.5.3" || modVersion == "1.5.4" || modVersion == "1.5.5" || modVersion == "1.5.6" || modVersion == "1.5.7" || modVersion == "1.5.8" || modVersion == "1.5.9")) ||
            (expectedMod == "1.5.11" && (modVersion == "1.5.1" || modVersion == "1.5.2" || modVersion == "1.5.3" || modVersion == "1.5.4" || modVersion == "1.5.5" || modVersion == "1.5.6" || modVersion == "1.5.7" || modVersion == "1.5.8" || modVersion == "1.5.9" || modVersion == "1.5.10")) ||
            (expectedMod == "1.5.12" && (modVersion == "1.5.1" || modVersion == "1.5.2" || modVersion == "1.5.3" || modVersion == "1.5.4" || modVersion == "1.5.5" || modVersion == "1.5.6" || modVersion == "1.5.7" || modVersion == "1.5.8" || modVersion == "1.5.9" || modVersion == "1.5.10" || modVersion == "1.5.11")) ||
            (expectedMod == "1.5.13" && (modVersion == "1.5.1" || modVersion == "1.5.2" || modVersion == "1.5.3" || modVersion == "1.5.4" || modVersion == "1.5.5" || modVersion == "1.5.6" || modVersion == "1.5.7" || modVersion == "1.5.8" || modVersion == "1.5.9" || modVersion == "1.5.10" || modVersion == "1.5.11" || modVersion == "1.5.12")) ||
            (expectedMod == "1.5.14" && (modVersion == "1.5.1" || modVersion == "1.5.2" || modVersion == "1.5.3" || modVersion == "1.5.4" || modVersion == "1.5.5" || modVersion == "1.5.6" || modVersion == "1.5.7" || modVersion == "1.5.8" || modVersion == "1.5.9" || modVersion == "1.5.10" || modVersion == "1.5.11" || modVersion == "1.5.12" || modVersion == "1.5.13")) ||
            (expectedMod == "1.5.15" && (modVersion == "1.5.1" || modVersion == "1.5.2" || modVersion == "1.5.3" || modVersion == "1.5.4" || modVersion == "1.5.5" || modVersion == "1.5.6" || modVersion == "1.5.7" || modVersion == "1.5.8" || modVersion == "1.5.9" || modVersion == "1.5.10" || modVersion == "1.5.11" || modVersion == "1.5.12" || modVersion == "1.5.13" || modVersion == "1.5.14")) ||
            (expectedMod == "1.5.16" && (modVersion == "1.5.1" || modVersion == "1.5.2" || modVersion == "1.5.3" || modVersion == "1.5.4" || modVersion == "1.5.5" || modVersion == "1.5.6" || modVersion == "1.5.7" || modVersion == "1.5.8" || modVersion == "1.5.9" || modVersion == "1.5.10" || modVersion == "1.5.11" || modVersion == "1.5.12" || modVersion == "1.5.13" || modVersion == "1.5.14" || modVersion == "1.5.15"));
        if (schema != CurrentSchema || !compatibleVersion || !IsHash(nativeSaveHash) ||
            checkpointSegment < 0 || checkpointSegment > 6 || !WireJson.Finite(checkpointTime) || checkpointTime < 0) return false;
        return (!snapshot.Enabled(14) || tidePhase.HasValue) && (!tidePhase.HasValue || VolcanoTide.ValidPhase(tidePhase.Value));
    }
    // Historical sidecar validation remains frozen to its original version family.
    private static bool CanReadLegacy26(string saved, string current)
    {
        bool Valid(string value)
        {
            if (value == null || !value.StartsWith("2.6.", StringComparison.Ordinal) || value.Length < 5) return false;
            for (int i = 4; i < value.Length; i++) if (value[i] < 'a' || value[i] > 'z') return false;
            return true;
        }
        return Valid(saved) && Valid(current) && (saved.Length < current.Length ||
            (saved.Length == current.Length && string.CompareOrdinal(saved, current) <= 0));
    }

    private const string FirstThreeXWriter = "1.5.22";

    // Strict three-part versions only ("1.5.22", not "1.5.22.0" or " 1.5.22").
    private static bool SameOrNewer(string value, string minimum) =>
        System.Version.TryParse(value, out var version) && version.ToString() == value && version.Revision == -1 &&
        System.Version.TryParse(minimum, out var floor) && version >= floor;

    private static bool KnownPriorVersion(string value, int minimum, int maximum = 16)
    {
        return System.Version.TryParse(value, out var version) && version.ToString() == value &&
            version.Major == 1 && version.Minor == 5 && version.Build >= minimum && version.Build <= maximum && version.Revision == -1;
    }

    private static bool IsHash(string value)
    {
        if (value == null || value.Length != 64) return false;
        foreach (char c in value) if (!Uri.IsHexDigit(c)) return false;
        return true;
    }
}
