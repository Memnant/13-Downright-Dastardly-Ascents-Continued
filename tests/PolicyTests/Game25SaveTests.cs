using dda;

internal static class Game25SaveTests
{
    internal static void Run(Action<bool, string> check)
    {
        var id = Guid.NewGuid();
        for (int patch = 0; patch <= 17; patch++)
        foreach (var selected in new[] { (0, 0), (9, 0), (14, 0), (18, 0), (20, 0), (0, 1 << 9) })
        foreach (bool honor in new[] { false, true })
        {
            var rules = new DifficultySnapshot(selected.Item1, selected.Item2, false, true, honor);
            var record = new SavedRuleRecord {
                schema = patch == 0 ? 1 : 2, runId = id.ToString("N"),
                gameVersion = patch == 17 ? "2.5.a" : "2.4.c", modVersion = "1.5." + patch,
                rules = rules.Encode(), nativeSaveHash = new string('A', 64), checkpointSegment = 3,
                checkpointTime = 456.25, tidePhase = 1.25
            };
            string original = record.ToJson();
            var copy = SavedRuleRecord.FromJson(original);
            check(copy.TryRead(id, "2.5.a", "1.5.17", out var loaded) && loaded.Encode() == rules.Encode(),
                "2.5 upgrade preserves all selected/forced rules and host options");
            check(copy.ToJson() == original && copy.tidePhase == 1.25 && copy.nativeSaveHash == record.nativeSaveHash,
                "migration is read-only and retains the exact checkpoint and lava phase");
            check(!copy.TryRead(Guid.NewGuid(), "2.5.a", "1.5.17", out _), "upgrade rejects different RunId");
            check(!copy.TryRead(id, "2.5.b", "1.5.17", out _), "unaudited game migration rejected");
            check(!copy.TryRead(id, "2.5.a", "1.6.0", out _), "unaudited mod migration rejected");
            if (patch == 17)
                check(!copy.TryRead(id, "2.4.c", "1.5.16", out _), "new game records cannot downgrade");
            if (patch > 0)
            {
                copy.nativeSaveHash = "damaged";
                check(!copy.TryRead(id, "2.5.a", "1.5.17", out _), "upgrade still rejects malformed native hash");
            }
        }
        var current = new DifficultySnapshot(20, 0, true, true);
        check(ReadinessPolicy.Check(current, "1.5.17", "1.5.16", current.Encode()) == PeerReadiness.VersionMismatch,
            "2.4 mod peer cannot join 2.5 extended challenge");
        check(ReadinessPolicy.Check(current, "1.5.17", "1.5.17", current.Encode()) == PeerReadiness.Ready,
            "matching 2.5 peers accepted");
    }
}
