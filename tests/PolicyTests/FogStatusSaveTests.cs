using dda;

internal static class FogStatusSaveTests
{
    internal static void Run(Action<bool, string> check)
    {
        var id = Guid.NewGuid();
        for (int patch = 0; patch <= 14; patch++)
        foreach (var selection in new[] { (13, 0), (14, 0), (17, 0), (18, 0), (20, 0), (0, 1 << 5), (0, 1 << 9) })
        {
            var expected = new DifficultySnapshot(selection.Item1, selection.Item2, true, false, true);
            var record = new SavedRuleRecord { schema = patch == 0 ? 1 : 2, runId = id.ToString("N"), gameVersion = "2.4.c",
                modVersion = "1.5." + patch, rules = expected.Encode(), nativeSaveHash = new string('C', 64),
                checkpointSegment = 2, checkpointTime = 800, tidePhase = 1.8 };
            var copy = SavedRuleRecord.FromJson(record.ToJson());
            check(copy.TryRead(id, "2.4.c", "1.5.14", out var loaded) && loaded.Encode() == expected.Encode() &&
                loaded.Enabled(14) == (selection.Item1 >= 14 || selection.Item2 == 1 << 5) &&
                loaded.Enabled(18) == (selection.Item1 >= 18 || selection.Item2 == 1 << 9) && copy.tidePhase == 1.8 &&
                copy.checkpointSegment == 2 && copy.nativeSaveHash == record.nativeSaveHash,
                "1.5.14 keeps the saved level; fog damage and movement use separate gates");
            check(!copy.TryRead(Guid.NewGuid(), "2.4.c", "1.5.14", out _), "fog update preserves RunId isolation");
            if (patch == 14) check(!copy.TryRead(id, "2.4.c", "1.5.13", out _), "downgrade requires paired backup");
        }
        var rules = new DifficultySnapshot(20, 0, true, true);
        check(ReadinessPolicy.Check(rules, "1.5.14", "1.5.14", rules.Encode()) == PeerReadiness.Ready, "matching fog version ready");
        check(ReadinessPolicy.Check(rules, "1.5.14", "1.5.13", rules.Encode()) == PeerReadiness.VersionMismatch, "mixed fog types rejected before extended run");
    }
}
