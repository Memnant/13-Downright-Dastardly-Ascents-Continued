using dda;

internal static class Game26SaveTests
{
    internal static void Run(Action<bool, string> check)
    {
        var id = Guid.NewGuid();
        foreach (int level in new[] { 0, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 })
        foreach (int forced in new[] { 0, 1 << 5, 1 << 11 })
        foreach (bool honor in new[] { false, true })
        {
            var rules = new DifficultySnapshot(level, forced, false, true, honor);
            var record = new SavedRuleRecord {
                schema = 2, runId = id.ToString("N"), gameVersion = "2.6.a", modVersion = "1.5.20",
                rules = rules.Encode(), nativeSaveHash = new string('B', 64), checkpointSegment = 4,
                checkpointTime = 678.5, tidePhase = 1.25
            };
            string json = record.ToJson();
            var copy = SavedRuleRecord.FromJson(json);
            check(copy.TryRead(id, "2.6.a", "1.5.20", out var restored) && restored.Encode() == rules.Encode(),
                "2.6 sidecar roundtrip preserves cumulative/forced rules and host option");
            check(copy.ToJson() == json && copy.tidePhase == 1.25 && copy.nativeSaveHash == record.nativeSaveHash,
                "2.6 checkpoint identity and lava phase remain unchanged");
            check(!copy.TryRead(Guid.NewGuid(), "2.6.a", "1.5.20", out _), "2.6 rejects another run");
            check(!copy.TryRead(id, "2.5.a", "1.5.19", out _), "2.6 saves cannot downgrade to 2.5");
            check(!copy.TryRead(id, "2.6.b", "1.5.20", out _), "unknown game save migration stays rejected");
            copy.nativeSaveHash = "damaged";
            check(!copy.TryRead(id, "2.6.a", "1.5.20", out _), "2.6 still requires a native checkpoint hash");
            for (int patch = 0; patch <= 19; patch++)
            {
                copy = SavedRuleRecord.FromJson(json);
                copy.gameVersion = patch >= 17 ? "2.5.a" : "2.4.c";
                copy.modVersion = "1.5." + patch;
                copy.schema = patch == 0 ? 1 : 2;
                string original = copy.ToJson();
                check(!copy.TryRead(id, "2.6.a", "1.5.20", out _), "2.6 rejects old native-format sidecars");
                check(copy.ToJson() == original, "old incompatible sidecars are not rewritten");
            }
        }
        var current = new DifficultySnapshot(20, 0, true, true);
        check(ReadinessPolicy.Check(current, "1.5.20", "1.5.19", current.Encode()) == PeerReadiness.VersionMismatch,
            "2.6 multiplayer requires all clients on the updated build");
        check(ReadinessPolicy.Check(current, "1.5.20", "1.5.20", current.Encode()) == PeerReadiness.Ready,
            "matching 2.6 peers are accepted");
    }
}
