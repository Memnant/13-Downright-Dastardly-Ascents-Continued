using dda;

internal static class HostOptionsTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (int level in new[] { 0 }.Concat(Enumerable.Range(9, 12)))
        foreach (int mask in new[] { 0, 1 << 11, DifficultySnapshot.AllowedMask })
        foreach (bool free in new[] { false, true })
        {
            var snapshot = new DifficultySnapshot(level, mask, false, true, free);
            check(snapshot.GrantsSummitHonor == (level >= 9 && free), "honor limited to selected extended levels, not force flags");
            check(DifficultySnapshot.TryDecode(snapshot.Encode(), out var restored) && restored.FreeSummitHonor == free &&
                restored.Ascent == level && restored.ForceMask == mask, "host option wire round trip");
            var different = new DifficultySnapshot(level, mask, false, true, !free);
            if (snapshot.HasEffects)
                check(ReadinessPolicy.Check(snapshot, "1.5.7", "1.5.7", different.Encode()) == PeerReadiness.RulesPending,
                    "option mismatch blocks departure");
        }
        foreach (string bad in new[] { "2:20:0:1:1", "2:20:0:1:1:2", "2:20:0:1:1:-1", "3:20:0:1:1:1", "2:20:0:1:1:1:0" })
            check(!DifficultySnapshot.TryDecode(bad, out _), "reject malformed host option");
        var id = Guid.NewGuid();
        for (int patch = 0; patch <= 6; patch++)
        {
            var record = new SavedRuleRecord { schema = patch == 0 ? 1 : 2, runId = id.ToString("N"), gameVersion = "2.4.c",
                modVersion = "1.5." + patch, rules = "1:20:4095:0:1", nativeSaveHash = new string('A', 64),
                checkpointSegment = 3, checkpointTime = 350, tidePhase = 1.8 };
            check(record.TryRead(id, "2.4.c", "1.5.7", out var legacy) && legacy.Ascent == 20 && legacy.ForceMask == 4095 &&
                !legacy.FreeSummitHonor && !legacy.NerfedRevives && legacy.CursePunishment,
                "old sidecar preserves rules and defaults new option off: " + patch);
        }
        foreach (bool enabled in new[] { false, true })
        {
            var record = new SavedRuleRecord { schema = 2, runId = id.ToString("N"), gameVersion = "2.4.c", modVersion = "1.5.7",
                rules = new DifficultySnapshot(20, 0, true, true, enabled).Encode(), nativeSaveHash = new string('A', 64),
                checkpointSegment = 3, checkpointTime = 350, tidePhase = 1.8 };
            var copy = SavedRuleRecord.FromJson(record.ToJson());
            check(copy.TryRead(id, "2.4.c", "1.5.7", out var loaded) && loaded.FreeSummitHonor == enabled, "resume restores host option");
            check(!copy.TryRead(Guid.NewGuid(), "2.4.c", "1.5.7", out _), "option cannot leak to different RunId");
            check(!copy.TryRead(id, "2.4.c", "1.5.6", out _), "downgrade needs paired backup");
        }
        var window = new OpeningProtectionState { generation = Guid.NewGuid().ToString("N"), clock = 100 };
        check(window.Valid && window.Remaining(100) == 0, "armed window gives no protection during airport/loading");
        window.started = true;
        var joined = WireJson.Decode<OpeningProtectionState>(WireJson.Encode(window));
        foreach (int fps in new[] { 30, 60, 144 })
        foreach (double elapsed in new[] { 0d, 1d / fps, 12d, 29.9, 30, 31, 300 })
            check(Math.Abs(joined.Remaining(100 + elapsed) - Math.Max(0, 30 - elapsed)) < 1e-8,
                "shared deadline survives late join/host handoff without restarting");
        check(joined.Remaining(115) == 15 && joined.Remaining(135) == 0, "reconnect only inherits remaining time");
        window.clock = 4294966.296;
        check(Math.Abs(window.Remaining(2) - 27) < 1e-8, "opening protection handles Photon clock rollover");
        window.clock = double.NaN; check(!window.Valid, "invalid protection clock rejected");
        window.clock = 0; window.generation = Guid.Empty.ToString("N"); check(!window.Valid, "empty generation rejected");
    }
}
