using dda;

internal static class ChasingFogSaveTests
{
    internal static void Run(Action<bool, string> check)
    {
        var id = Guid.NewGuid();
        for (int patch = 0; patch <= 11; patch++)
        {
            var record = new SavedRuleRecord { schema = patch == 0 ? 1 : 2, runId = id.ToString("N"), gameVersion = "2.4.c",
                modVersion = "1.5." + patch, rules = new DifficultySnapshot(20, 0, false, true, true).Encode(),
                nativeSaveHash = new string('A', 64), checkpointSegment = 3, checkpointTime = 350, tidePhase = 1.8 };
            var copy = SavedRuleRecord.FromJson(record.ToJson());
            check(copy.TryRead(id, "2.4.c", "1.5.11", out var rules) && rules.Enabled(18) && rules.Ascent == 20 &&
                rules.FreeSummitHonor && !rules.NerfedRevives && rules.CursePunishment && copy.tidePhase == 1.8 &&
                copy.checkpointSegment == 3 && copy.nativeSaveHash == record.nativeSaveHash,
                "1.5.11 keeps prior rules/checkpoint/tide and uses tier 18 for chasing fog");
            check(!copy.TryRead(Guid.NewGuid(), "2.4.c", "1.5.11", out _), "1.5.11 RunId isolation");
            if (patch == 11) check(!copy.TryRead(id, "2.4.c", "1.5.10", out _), "1.5.11 requires paired save for downgrade");
        }
        foreach (int level in new[] { 9, 17, 18, 20 })
        foreach (int mask in new[] { 0, 1, 1 << 9 })
        {
            var record = new SavedRuleRecord { schema = 2, runId = id.ToString("N"), gameVersion = "2.4.c", modVersion = "1.5.10",
                rules = new DifficultySnapshot(level, mask, true, true).Encode(), nativeSaveHash = new string('A', 64),
                checkpointSegment = 3, checkpointTime = 350, tidePhase = 1.8 };
            check(record.TryRead(id, "2.4.c", "1.5.11", out var rules) && rules.Ascent == level && rules.ForceMask == mask &&
                rules.Enabled(18) == (level >= 18 || mask == 1 << 9), "old selected/forced 9 does not migrate into forced 18");
        }
        var selected = new DifficultySnapshot(18, 0, true, true);
        check(ReadinessPolicy.Check(selected, "1.5.11", "1.5.11", selected.Encode()) == PeerReadiness.Ready, "same fog version ready");
        check(ReadinessPolicy.Check(selected, "1.5.11", "1.5.10", selected.Encode()) == PeerReadiness.VersionMismatch, "old fog tier is rejected for extended runs");
    }
}
