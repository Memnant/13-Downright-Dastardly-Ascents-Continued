using dda;

internal static class Game30SaveTests
{
    internal static void Run(Action<bool, string> check)
    {
        check(GameVersionPolicy.CanReadSave("3.0.2", "3.0.10"), "numeric patches compare numerically");
        check(!GameVersionPolicy.CanReadSave("3.0.10", "3.0.2"), "numeric patch downgrade is rejected");
        check(GameVersionPolicy.CanReadSave("3.0.0--logFile", "3.0.0"), "audited build alias has the same save version");
        var id = Guid.NewGuid();
        foreach (int level in new[] { 0, 9, 12, 13, 14, 18, 20 })
        foreach (int force in new[] { 0, 1 << 5, 1 << 11 })
        foreach (bool honor in new[] { false, true })
        {
            var rules = new DifficultySnapshot(level, force, true, true, honor);
            var record = new SavedRuleRecord { schema = 2, runId = id.ToString("N"), gameVersion = "3.0.0",
                modVersion = "1.5.22", nativeSaveVersion = 4, rules = rules.Encode(), nativeSaveHash = new string('A', 64),
                checkpointSegment = 3, checkpointTime = 500, tidePhase = 1.25 };
            string original = record.ToJson();
            var copy = SavedRuleRecord.FromJson(original);
            foreach (string game in new[] { "3.0.0", "3.0.0--logFile", "3.0.1", "3.0.10" })
                check(copy.TryRead(id, game, "1.5.22", out var restored) && restored.Encode() == rules.Encode(), "3.0 rules survive same-series patch upgrades");
            check(copy.ToJson() == original, "3.0 reads do not rewrite records");
            check(!copy.TryRead(Guid.NewGuid(), "3.0.0", "1.5.22", out _), "3.0 RunId isolation remains");
            check(!copy.TryRead(id, "3.1.0", "1.5.22", out _), "unverified minor-series migration rejected");
            check(!copy.TryRead(id, "2.6.a", "1.5.21", out _), "3.0 record cannot downgrade to 2.6 mod");
            foreach (int? format in new int?[] { null, 3, 5 })
            {
                copy.nativeSaveVersion = format;
                check(!copy.TryRead(id, "3.0.0", "1.5.22", out _), "3.0 requires explicit native format 4");
            }
            copy.nativeSaveVersion = 4;
            copy.gameVersion = "2.6.a"; copy.modVersion = "1.5.21";
            check(!copy.TryRead(id, "3.0.0", "1.5.22", out _), "old map-pool save is not relabelled as a 3.0 save");
            copy.gameVersion = "3.0.0"; copy.modVersion = "1.5.22"; copy.nativeSaveHash = "bad";
            check(!copy.TryRead(id, "3.0.0", "1.5.22", out _), "3.0 retains checkpoint hash validation");
        }
    }
}
