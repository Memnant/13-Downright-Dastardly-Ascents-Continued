using dda;

internal static class BalanceSaveTests
{
    internal static void Run(Action<bool, string> check)
    {
        var id = Guid.NewGuid();
        for (int patch = 0; patch <= 12; patch++)
        {
            var record = new SavedRuleRecord { schema = patch == 0 ? 1 : 2, runId = id.ToString("N"), gameVersion = "2.4.c",
                modVersion = "1.5." + patch, rules = new DifficultySnapshot(20, 0, false, true, true).Encode(),
                nativeSaveHash = new string('A', 64), checkpointSegment = 3, checkpointTime = 350, tidePhase = 1.8 };
            var copy = SavedRuleRecord.FromJson(record.ToJson());
            check(copy.TryRead(id, "2.4.c", "1.5.12", out var rules) && rules.Ascent == 20 &&
                rules.FreeSummitHonor && !rules.NerfedRevives && rules.CursePunishment && copy.tidePhase == 1.8 &&
                copy.checkpointSegment == 3 && copy.nativeSaveHash == record.nativeSaveHash,
                "1.5.12 retains prior selections, checkpoint and tide metadata");
            check(!copy.TryRead(Guid.NewGuid(), "2.4.c", "1.5.12", out _), "balance update preserves RunId isolation");
            if (patch == 12) check(!copy.TryRead(id, "2.4.c", "1.5.11", out _), "downgrade requires paired pre-upgrade save");
        }
        var selected = new DifficultySnapshot(20, 0, true, true);
        check(ReadinessPolicy.Check(selected, "1.5.12", "1.5.12", selected.Encode()) == PeerReadiness.Ready, "same balance version ready");
        check(ReadinessPolicy.Check(selected, "1.5.12", "1.5.11", selected.Encode()) == PeerReadiness.VersionMismatch, "mixed fog/poison timings rejected for extended runs");
        foreach (float rest in new[] { 29f, 30f, 60f, 90f, 91f, 120f, 121f })
        {
            var phase = new SnowWeatherState { runId = id.ToString("N"), active = false, duration = rest, clock = 10, direction = 1 };
            var copy = WireJson.Decode<SnowWeatherState>(WireJson.Encode(phase));
            check(copy.Valid == (rest >= 30 && rest <= 120), "network snow rest accepts both normal and swamp host phases");
            if (copy.Valid) check(copy.Remaining(20) == rest - 10, "late join inherits remaining rest, not a fresh timer");
        }
    }
}
