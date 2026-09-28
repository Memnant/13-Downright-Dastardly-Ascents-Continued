using dda;

internal static class SwampBalanceSaveTests
{
    internal static void Run(Action<bool, string> check)
    {
        var id = Guid.NewGuid();
        for (int patch = 0; patch <= 13; patch++)
        foreach (var selection in new[] { (17, 0), (18, 0), (19, 0), (20, 0), (0, 1 << 11), (19, 1 << 11) })
        {
            var rules = new DifficultySnapshot(selection.Item1, selection.Item2, false, true, true);
            var record = new SavedRuleRecord { schema = patch == 0 ? 1 : 2, runId = id.ToString("N"), gameVersion = "2.4.c",
                modVersion = "1.5." + patch, rules = rules.Encode(), nativeSaveHash = new string('B', 64),
                checkpointSegment = 3, checkpointTime = 100, tidePhase = 1.8 };
            var copy = SavedRuleRecord.FromJson(record.ToJson());
            check(copy.TryRead(id, "2.4.c", "1.5.13", out var loaded) && loaded.Encode() == rules.Encode() &&
                copy.checkpointSegment == 3 && copy.tidePhase == 1.8, "1.5.13 reads prior records without changing selection or checkpoint");
            check(!copy.TryRead(Guid.NewGuid(), "2.4.c", "1.5.13", out _), "swamp update preserves RunId isolation");
            if (patch == 13) check(!copy.TryRead(id, "2.4.c", "1.5.12", out _), "older plugin requires paired backup for downgrade");
        }
        var selected = new DifficultySnapshot(20, 0, true, true);
        check(ReadinessPolicy.Check(selected, "1.5.13", "1.5.13", selected.Encode()) == PeerReadiness.Ready, "matching swamp version ready");
        check(ReadinessPolicy.Check(selected, "1.5.13", "1.5.12", selected.Encode()) == PeerReadiness.VersionMismatch, "mixed sleep and snow rules rejected");
        foreach (float rest in new[] { 30f, 60f, 90f, 120f })
        {
            var phase = WireJson.Decode<SnowWeatherState>(WireJson.Encode(new SnowWeatherState {
                runId = id.ToString("N"), active = false, duration = rest, clock = 100, direction = -1 }));
            check(phase.Valid && phase.Remaining(110) == rest - 10, "late join or handoff inherits extended snow rest");
            check(phase.Remaining(500) == 0, "expired shared phase cannot keep snow waiting indefinitely");
        }
    }
}
