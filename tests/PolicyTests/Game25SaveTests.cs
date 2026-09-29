using dda;

internal static class Game25SaveTests
{
    internal static void Run(Action<bool, string> check)
    {
        var id = Guid.NewGuid();
        foreach (int target in new[] { 17, 18, 19 })
        for (int patch = 0; patch <= target; patch++)
        foreach (var selected in new[] { (0, 0), (9, 0), (14, 0), (18, 0), (20, 0), (0, 1 << 9) })
        foreach (bool honor in new[] { false, true })
        {
            var rules = new DifficultySnapshot(selected.Item1, selected.Item2, false, true, honor);
            var record = new SavedRuleRecord {
                schema = patch == 0 ? 1 : 2, runId = id.ToString("N"),
                gameVersion = patch >= 17 ? "2.5.a" : "2.4.c", modVersion = "1.5." + patch,
                rules = rules.Encode(), nativeSaveHash = new string('A', 64), checkpointSegment = 3,
                checkpointTime = 456.25, tidePhase = 1.25
            };
            string original = record.ToJson();
            var copy = SavedRuleRecord.FromJson(original);
            string targetVersion = "1.5." + target;
            check(copy.TryRead(id, "2.5.a", targetVersion, out var loaded) && loaded.Encode() == rules.Encode(),
                "2.5 upgrade preserves all selected/forced rules and host options");
            check(copy.ToJson() == original && copy.tidePhase == 1.25 && copy.nativeSaveHash == record.nativeSaveHash,
                "migration is read-only and retains the exact checkpoint and lava phase");
            check(!copy.TryRead(Guid.NewGuid(), "2.5.a", targetVersion, out _), "upgrade rejects different RunId");
            check(!copy.TryRead(id, "2.5.b", targetVersion, out _), "unaudited game migration rejected");
            check(!copy.TryRead(id, "2.5.a", "1.6.0", out _), "unaudited mod migration rejected");
            if (patch >= 17)
                check(!copy.TryRead(id, "2.4.c", "1.5.16", out _), "new game records cannot downgrade");
            if (patch >= 18)
                check(!copy.TryRead(id, "2.5.a", "1.5.17", out _), "new mod records cannot silently downgrade");
            if (patch == 19)
                check(!copy.TryRead(id, "2.5.a", "1.5.18", out _), "weather release records cannot silently downgrade");
            if (patch > 0)
            {
                copy.nativeSaveHash = "damaged";
                check(!copy.TryRead(id, "2.5.a", targetVersion, out _), "upgrade still rejects malformed native hash");
            }
        }
        var current = new DifficultySnapshot(20, 0, true, true);
        check(ReadinessPolicy.Check(current, "1.5.17", "1.5.16", current.Encode()) == PeerReadiness.VersionMismatch,
            "2.4 mod peer cannot join 2.5 extended challenge");
        check(ReadinessPolicy.Check(current, "1.5.17", "1.5.17", current.Encode()) == PeerReadiness.Ready,
            "matching 2.5 peers accepted");
        check(ReadinessPolicy.Check(current, "1.5.18", "1.5.17", current.Encode()) == PeerReadiness.VersionMismatch,
            "multiplayer repair requires matching updated peers");
        check(ReadinessPolicy.Check(current, "1.5.18", "1.5.18", current.Encode()) == PeerReadiness.Ready,
            "matching fixed peers accepted");
        check(ReadinessPolicy.Check(current, "1.5.19", "1.5.18", current.Encode()) == PeerReadiness.VersionMismatch,
            "all peers need the persistent native weather protocol");
        check(ReadinessPolicy.Check(current, "1.5.19", "1.5.19", current.Encode()) == PeerReadiness.Ready,
            "matching weather protocol peers accepted");
    }
}
