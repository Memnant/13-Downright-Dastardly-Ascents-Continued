using dda;

internal static class WeatherGraceTests
{
    internal static void Run(Action<bool, string> check)
    {
        var id = Guid.NewGuid();
        string key = id.ToString("N");
        var pending = new WeatherGraceState { generation = Guid.NewGuid().ToString("N") };
        check(pending.Valid && pending.Blocks(key, 0), "weather is held before wake-up");
        check(pending.Remaining(3600) == 30 && pending.Blocks(key, 3600), "long loading does not consume grace");
        check(!pending.TryStart(Guid.Empty, 100), "wait for actual RunId");
        check(pending.TryStart(id, 100), "wake starts one window");
        check(!pending.TryStart(id, 110) && pending.clock == 100, "repeated initialization cannot extend grace");
        check(!pending.TryStart(Guid.NewGuid(), 110), "other run cannot reuse old window");
        foreach (int fps in new[] { 30, 60, 144 })
        foreach (double elapsed in new[] { 0d, 1d / fps, 10d, 29.999d, 30d, 31d, 90d })
        {
            var peer = WireJson.Decode<WeatherGraceState>(WireJson.Encode(pending));
            check(Math.Abs(peer.Remaining(100 + elapsed) - Math.Max(0, 30 - elapsed)) < 1e-8,
                "shared deadline independent of FPS/late join/migration");
            check(peer.Blocks(key, 100 + elapsed) == (elapsed < 30), "weather resumes at 30 seconds");
        }
        check(!pending.Blocks(Guid.NewGuid().ToString("N"), 100), "grace does not leak to unrelated RunId");
        var resume = new WeatherGraceState { generation = Guid.NewGuid().ToString("N"), runId = key };
        check(resume.Blocks(Guid.NewGuid().ToString("N"), 500), "resume preparation holds old scene weather");
        check(!resume.TryStart(Guid.NewGuid(), 500), "old scene RunId cannot start pending resume");
        check(resume.TryStart(id, 900) && resume.Remaining(900) == 30, "same checkpoint gets fresh grace only on explicit resume");
        var migrated = WireJson.Decode<WeatherGraceState>(WireJson.Encode(resume));
        check(!migrated.TryStart(id, 910) && migrated.Remaining(910) == 20, "host migration keeps deadline");
        resume.clock = 4294966.296;
        check(Math.Abs(resume.Remaining(2) - 27) < 1e-8, "Photon clock rollover");
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, -1 })
        {
            pending.clock = invalid;
            check(!pending.Valid && !pending.Blocks(key, 100), "reject invalid state clock");
        }
        pending.clock = 0; pending.runId = null;
        check(!pending.Valid, "started state needs run identity");
        pending.started = false; pending.generation = Guid.Empty.ToString("N");
        check(!pending.Valid, "reject empty generation");
        foreach (int patch in Enumerable.Range(0, 8))
        foreach (bool free in new[] { false, true })
        {
            var record = new SavedRuleRecord { schema = patch == 0 ? 1 : 2, runId = key, gameVersion = "2.4.c",
                modVersion = "1.5." + patch, rules = patch < 7 ? "1:20:4095:0:1" : new DifficultySnapshot(20, 4095, false, true, free).Encode(),
                nativeSaveHash = new string('A', 64), checkpointSegment = 3, checkpointTime = 350, tidePhase = 1.8 };
            check(record.TryRead(id, "2.4.c", "1.5.8", out var rules) && rules.Ascent == 20 && rules.ForceMask == 4095 &&
                !rules.NerfedRevives && rules.CursePunishment && rules.FreeSummitHonor == (patch >= 7 && free),
                "upgrade retains prior rules, tide, and summit option");
        }
        var current = new SavedRuleRecord { schema = 2, runId = key, gameVersion = "2.4.c", modVersion = "1.5.8",
            rules = new DifficultySnapshot(20, 0, true, true, true).Encode(), nativeSaveHash = new string('B', 64),
            checkpointSegment = 3, checkpointTime = 600, tidePhase = 2.2 };
        var restored = SavedRuleRecord.FromJson(current.ToJson());
        check(restored.TryRead(id, "2.4.c", "1.5.8", out var snapshot) && snapshot.FreeSummitHonor && restored.tidePhase == 2.2,
            "current sidecar round trip");
        check(!restored.TryRead(id, "2.4.c", "1.5.7", out _), "downgrade requires paired backup");
    }
}
