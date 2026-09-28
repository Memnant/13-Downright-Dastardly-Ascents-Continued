using dda;

internal static class RegressionTests
{
    internal static void Run(Action<bool, string> check)
    {
        void Near(double actual, double expected, string why) => check(Math.Abs(actual - expected) < 1e-8, why);
        Near(VolcanoTide.Height(0), 779, "legacy low tide height");
        Near(VolcanoTide.Height(Math.Sqrt(Math.PI)), 795, "legacy high tide height");
        Near(VolcanoTide.Advance(0, 17.8), 1, "normal phase speed");
        Near(VolcanoTide.Advance(1.75, 17.8 * .05 / .15), 1.8, "high tide slow band");
        Near(VolcanoTide.Advance(1.85, 17.8 * .05), 1.9, "normal speed after high tide");
        Near(VolcanoTide.Advance(VolcanoTide.MinimumPhase, VolcanoTide.Period), VolcanoTide.MinimumPhase, "complete cycle resets");
        foreach (double phase in new[] { -.35, 0, 1.75, 1.8, 1.85, 2.5 })
        foreach (double time in new[] { 0, .001, 3, 30, 100, 1e5 })
        {
            double a = VolcanoTide.Advance(phase, time);
            check(VolcanoTide.ValidPhase(a), "phase remains in range");
            Near(a, VolcanoTide.Advance(VolcanoTide.Advance(phase, time / 3), time * 2 / 3), "checkpoint/host handoff preserves phase");
        }
        var id = Guid.NewGuid();
        var anchor = new TideAnchor { runId = id.ToString("N"), phase = 1.72, clock = 250, running = true };
        var joining = WireJson.Decode<TideAnchor>(WireJson.Encode(anchor));
        check(joining.Valid, "joined client reads room anchor");
        double expected = anchor.At(310);
        foreach (int fps in new[] { 30, 60, 144 })
        {
            // Different local call counts and load delays cannot advance shared state.
            for (int frame = fps * 3; frame <= fps * 60; frame++)
            {
                double now = 250 + (double)frame / fps;
                Near(joining.At(now), anchor.At(now), "shared clock at FPS " + fps);
                Near(joining.At(now), joining.At(now), "multiple lava components are idempotent");
            }
            Near(joining.At(310), expected, "same final height despite delayed load");
        }
        Near(WireJson.Elapsed(2, 4294966.296), 3, "Photon clock rollover");
        anchor.running = false;
        Near(anchor.At(5000), anchor.phase, "paused biome phase does not advance");
        var rules = new DifficultySnapshot(17, 0, true, true);
        var record = new SavedRuleRecord { schema = 2, runId = id.ToString("N"), gameVersion = "2.4.c", modVersion = "1.5.1",
            rules = rules.Encode(), nativeSaveHash = new string('A', 64), checkpointSegment = 3, checkpointTime = 350, tidePhase = 1.8 };
        var restored = SavedRuleRecord.FromJson(record.ToJson());
        check(restored.TryRead(id, "2.4.c", "1.5.1", out var restoredRules) && restoredRules.Encode() == rules.Encode(), "v2 sidecar round trip");
        Near(restored.tidePhase.Value, 1.8, "v2 lava phase preserved");
        check(restored.TryRead(id, "2.4.c", "1.5.2", out var migrated) && migrated.Encode() == rules.Encode(), "1.5.1 to 1.5.2 keeps rules");
        check(restored.TryRead(id, "2.4.c", "1.5.3", out _), "1.5.1 to rebased 1.5.3 keeps checkpoint");
        var originalJson = restored.ToJson();
        check(!restored.TryRead(id, "2.4.c", "1.6.0", out _), "unknown future version rejected");
        check(restored.ToJson() == originalJson, "reading old metadata does not rewrite checkpoint");
        restored.modVersion = "1.5.2";
        check(restored.TryRead(id, "2.4.c", "1.5.2", out _), "1.5.2 checkpoint round trip");
        check(restored.TryRead(id, "2.4.c", "1.5.3", out _), "1.5.2 to rebased 1.5.3 keeps checkpoint");
        check(ReadinessPolicy.Check(rules, "1.5.3", "1.5.2", rules.Encode()) == PeerReadiness.VersionMismatch, "rebase requires all peers on 1.5.3");
        check(!restored.TryRead(id, "2.4.c", "1.5.1", out _), "downgrade needs matching backup");
        check(ReadinessPolicy.Check(rules, "1.5.2", "1.5.1", rules.Encode()) == PeerReadiness.VersionMismatch, "1.5.2 does not allow 1.5.1 peers");
        foreach (string version in new[] { "1.5.1", "1.5.2", "1.5.3", "1.5.4" })
        {
            restored.modVersion = version;
            check(restored.TryRead(id, "2.4.c", "1.5.4", out var weatherRules) && weatherRules.Encode() == rules.Encode(), "1.5.4 reads supported checkpoint " + version);
            check(restored.checkpointSegment == 3 && restored.checkpointTime == 350 && restored.tidePhase == 1.8, "weather fix preserves checkpoint state");
        }
        check(!restored.TryRead(id, "2.4.c", "1.5.3", out _), "1.5.4 downgrade requires paired backup");
        check(ReadinessPolicy.Check(rules, "1.5.4", "1.5.3", rules.Encode()) == PeerReadiness.VersionMismatch, "weather fix requires peers with matching weather");
        foreach (string version in new[] { "1.5.1", "1.5.2", "1.5.3", "1.5.4", "1.5.5" })
        {
            restored.modVersion = version;
            check(restored.TryRead(id, "2.4.c", "1.5.5", out var recoveryRules) && recoveryRules.Encode() == rules.Encode(), "1.5.5 reads supported checkpoint " + version);
            check(restored.checkpointSegment == 3 && restored.checkpointTime == 350 && restored.tidePhase == 1.8, "regional recovery preserves checkpoint state");
        }
        check(!restored.TryRead(id, "2.4.c", "1.5.4", out _), "1.5.5 downgrade requires paired backup");
        check(ReadinessPolicy.Check(rules, "1.5.5", "1.5.4", rules.Encode()) == PeerReadiness.VersionMismatch, "regional recovery requires matching peer version");
        foreach (string version in new[] { "1.5.1", "1.5.2", "1.5.3", "1.5.4", "1.5.5", "1.5.6" })
        {
            restored.modVersion = version;
            check(restored.TryRead(id, "2.4.c", "1.5.6", out var nativeSnowRules) && nativeSnowRules.Encode() == rules.Encode(), "1.5.6 preserves checkpoint " + version);
            check(restored.checkpointSegment == 3 && restored.checkpointTime == 350 && restored.tidePhase == 1.8, "native snow preserves saved run state");
        }
        check(!restored.TryRead(id, "2.4.c", "1.5.5", out _), "1.5.6 downgrade requires paired backup");
        check(ReadinessPolicy.Check(rules, "1.5.6", "1.5.5", rules.Encode()) == PeerReadiness.VersionMismatch, "swamp balance change requires matching peers");
        check(!record.TryRead(Guid.NewGuid(), "2.4.c", "1.5.1", out _), "checkpoint run isolation");
        record.nativeSaveHash = null;
        check(!record.TryRead(id, "2.4.c", "1.5.1", out _), "missing native checkpoint digest rejected");
        record.nativeSaveHash = new string('A', 64);
        record.tidePhase = null;
        check(!record.TryRead(id, "2.4.c", "1.5.1", out _), "missing tide state rejected for ascent 14+");
        record.tidePhase = double.NaN;
        check(!record.TryRead(id, "2.4.c", "1.5.1", out _), "NaN phase rejected");
        record.tidePhase = 100;
        check(!record.TryRead(id, "2.4.c", "1.5.1", out _), "out of range phase rejected");
        check(ReadinessPolicy.Check(rules, "1.5.1", "1.5.0", rules.Encode()) == PeerReadiness.VersionMismatch, "previous mod version cannot join an extended departure");
        var one = new ScoutPosition(1, 0, 0, 0);
        var ahead = new ScoutPosition(2, 0, 151, 0);
        var close = new ScoutPosition(3, 0, 160, 0);
        check(RuleZeroPolicy.PickTarget(new[] { one }) == -1, "single player never targeted");
        check(RuleZeroPolicy.PickTarget(new[] { one, ahead }) == 2, "isolated leader targeted");
        check(RuleZeroPolicy.PickTarget(new[] { one, ahead, close }) == -1, "a nearby teammate prevents retrigger even with a far third scout");
        check(RuleZeroPolicy.ShouldRelease(new[] { one, ahead, close }, 2), "reunion releases locked leader");
        check(!RuleZeroPolicy.ShouldRelease(new[] { one, ahead }, 2), "isolated leader retains lease");
        check(RuleZeroPolicy.ShouldRelease(new[] { one }, 2), "departed/dead target releases lease");
        check(RuleZeroPolicy.PickTarget(new[] { one, new ScoutPosition(2, 0, 0, -151) }) == 2, "legacy abs z trigger retained");
        var lease = new ScoutmasterLease { runId = id.ToString("N"), generation = Guid.NewGuid().ToString("N"), sequence = 2, scoutId = 50, targetId = 2, active = true, clock = 123 };
        check(WireJson.Decode<ScoutmasterLease>(WireJson.Encode(lease)).Valid, "scout lease round trip");
        foreach (int segment in new[] { -1, 0, 1, 2, 3, 4, 5, 6 })
            check(WeatherPolicy.InWeatherSegment(segment) == (segment >= 0 && segment <= 3), "weather only before Citadel/Kiln");
        var snow = new SnowWeatherState { runId = id.ToString("N"), clock = 100, active = true, duration = 20, direction = -1 };
        var receivedSnow = WireJson.Decode<SnowWeatherState>(WireJson.Encode(snow));
        check(receivedSnow.Valid, "shared snow clock validates");
        Near(receivedSnow.Remaining(108), 12, "late join snow phase");
        Near(receivedSnow.Remaining(121), 0, "expired phase clamps to zero");
        Near(receivedSnow.Elapsed(108), snow.Elapsed(108), "host migration retains clock and direction");
        snow.clock = 4294966.296;
        Near(snow.Elapsed(2), 3, "snow clock handles Photon rollover");
        snow.duration = 100; check(!snow.Valid, "invalid snow duration rejected");
        snow.duration = 20; snow.direction = 0; check(!snow.Valid, "invalid snow direction rejected");
    }
}
