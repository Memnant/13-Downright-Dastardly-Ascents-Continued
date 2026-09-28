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
        if (expectedRun == Guid.Empty || runId != expectedRun.ToString("N") || gameVersion != expectedGame ||
            !DifficultySnapshot.TryDecode(rules, out snapshot)) return false;
        if (schema == 1) return modVersion == "1.5.0" &&
            (expectedMod == "1.5.0" || expectedMod == "1.5.1" || expectedMod == "1.5.2" || expectedMod == "1.5.3" || expectedMod == "1.5.4" || expectedMod == "1.5.5" || expectedMod == "1.5.6" || expectedMod == "1.5.7" || expectedMod == "1.5.8" || expectedMod == "1.5.9" || expectedMod == "1.5.10" || expectedMod == "1.5.11" || expectedMod == "1.5.12" || expectedMod == "1.5.13" || expectedMod == "1.5.14" || expectedMod == "1.5.15" || expectedMod == "1.5.16");
        bool compatibleVersion = modVersion == expectedMod || (modVersion == "1.5.1" && expectedMod == "1.5.2") ||
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
    private static bool IsHash(string value)
    {
        if (value == null || value.Length != 64) return false;
        foreach (char c in value) if (!Uri.IsHexDigit(c)) return false;
        return true;
    }
}
