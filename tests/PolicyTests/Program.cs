using System.Globalization;
using System.Text.Json;
using dda;

int checks = 0;
void Check(bool pass, string scenario)
{
    checks++;
    if (!pass) throw new Exception("FAILED: " + scenario);
}
void Reject(Action action, string scenario)
{
    try { action(); } catch (ArgumentOutOfRangeException) { checks++; return; }
    throw new Exception("FAILED: accepted " + scenario);
}

// Full matrix catches renumbering, off-by-one thresholds and force-mask bleed into adjacent levels.
foreach (int selected in new[] { 0 }.Concat(Enumerable.Range(9, 12)))
foreach (int forced in new[] { -1 }.Concat(Enumerable.Range(9, 12)))
{
    var rules = new DifficultySnapshot(selected, forced < 0 ? 0 : 1 << (forced - 9), true, false);
    for (int tested = -1; tested <= 21; tested++)
        Check(rules.Enabled(tested) == (tested >= 9 && tested <= 20 && (tested <= selected || tested == forced)),
            $"selected={selected}, forced={forced}, effect={tested}");
    Check(DifficultySnapshot.TryDecode(rules.Encode(), out var restored) && rules.Encode() == restored.Encode(), "network round trip");
}
foreach (int invalid in new[] { -1, 1, 7, 8, 21, int.MaxValue })
    Reject(() => new DifficultySnapshot(invalid, 0, true, true), "invalid ascent " + invalid);
foreach (int invalid in new[] { -1, 4096, 8191, int.MaxValue })
    Reject(() => new DifficultySnapshot(9, invalid, true, true), "invalid force mask");
foreach (string malformed in new[] { "", "1:8:0:1:1", "2:9:0:1:1", "1:9:4096:1:1", "1:9:0:2:1", "1:9:0:-1:1", "1:9:0:1", "1:9:0:1:1:0", " 1:9:0:1:1", "1:+9:0:1:1", "1:2147483648:0:1:1", "1:9:0:1:1\n", "1:9:NaN:1:1" })
    Check(!DifficultySnapshot.TryDecode(malformed, out _), "reject malformed rules " + malformed);
foreach (string culture in new[] { "zh-CN", "ar-SA", "fr-FR", "en-US" })
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
    Check(new DifficultySnapshot(20, 4095, true, false).Encode() == "2:20:4095:1:0:0", "culture independent wire encoding");
}
for (int slot = 0; slot < 16; slot++)
{
    bool[] legacy = new bool[16]; legacy[slot] = true;
    Check(DifficultySnapshot.MigrateLegacyMask(legacy) == (slot is >= 1 and <= 12 ? 1 << (slot - 1) : 0), "ignore removed legacy slot " + slot);
}

var active = new DifficultySnapshot(16, 1 << 11, false, true);
Check(ReadinessPolicy.Check(active, "1.5.0", "1.5.0", active.Encode()) == PeerReadiness.Ready, "matching peer");
Check(ReadinessPolicy.Check(active, "1.5.0", "1.3.1", active.Encode()) == PeerReadiness.VersionMismatch, "old peer rejected");
Check(ReadinessPolicy.Check(active, "1.5.0", null!, null!) == PeerReadiness.VersionMismatch, "unmodded peer rejected");
Check(ReadinessPolicy.Check(active, "1.5.0", "1.5.0", new DifficultySnapshot(16, 0, false, true).Encode()) == PeerReadiness.RulesPending, "config mismatch blocks start");
Check(ReadinessPolicy.Check(DifficultySnapshot.Official, "1.5.0", null!, null!) == PeerReadiness.Ready, "vanilla peers allowed in vanilla mode");
Check(ReadinessPolicy.Check(new DifficultySnapshot(0, 1, true, true), "1.5.0", null!, null!) == PeerReadiness.VersionMismatch, "forced effects also require matching peer");

var id = Guid.NewGuid();
var saveRecord = new SavedRuleRecord { schema = 1, runId = id.ToString("N"), gameVersion = "2.4.c", modVersion = "1.5.0", rules = active.Encode() };
var restoredRecord = SavedRuleRecord.FromJson(saveRecord.ToJson());
Check(restoredRecord.TryRead(id, "2.4.c", "1.5.0", out var restoredRules) && restoredRules.Encode() == active.Encode(), "RunId sidecar restores exact configuration");
Check(!saveRecord.TryRead(Guid.NewGuid(), "2.4.c", "1.5.0", out _), "sidecar cannot leak to different run");
Check(!saveRecord.TryRead(id, "2.5", "1.5.0", out _), "game-version mismatch rejected");
Check(saveRecord.TryRead(id, "2.4.c", "1.5.1", out _), "1.5.0 sidecar migration accepted");
Check(saveRecord.TryRead(id, "2.4.c", "1.5.2", out _), "1.5.0 sidecar migration to 1.5.2 accepted");
Check(saveRecord.TryRead(id, "2.4.c", "1.5.3", out _), "1.5.0 sidecar migration to rebased 1.5.3 accepted");
Check(saveRecord.TryRead(id, "2.4.c", "1.5.4", out _), "1.5.0 sidecar migration to weather fix accepted");
Check(saveRecord.TryRead(id, "2.4.c", "1.5.6", out _), "1.5.0 sidecar migration to native snow accepted");
Check(!saveRecord.TryRead(id, "2.4.c", "1.6.0", out _), "unknown mod-version mismatch rejected");
Check(!new SavedRuleRecord().TryRead(id, "2.4.c", "1.5.0", out _), "missing JSON fields rejected");
saveRecord.rules = "1:8:0:1:1";
Check(!saveRecord.TryRead(id, "2.4.c", "1.5.0", out _), "old untracked extended save not migrated");

RegressionTests.Run(Check);
HostOptionsTests.Run(Check);
WeatherGraceTests.Run(Check);
TornadoRecoverySaveTests.Run(Check);
ChasingFogSaveTests.Run(Check);
BalanceSaveTests.Run(Check);
SwampBalanceSaveTests.Run(Check);
FogStatusSaveTests.Run(Check);
SwampFogSaveTests.Run(Check);
object allocation = args.Length > 1 ? AllocationRegression.Run(args[1], Check) : null;
var report = new { Status = "PASSED_POLICY", Assertions = checks, EngineExecuted = false, MultiplayerExecuted = false,
    AllocationBenchmark = allocation,
    Scenarios = new[] { "levels and cumulative/forced effects", "legacy removed flags", "network encoding and malformed input", "peer version/config readiness", "RunId sidecar isolation", "1.5.0 migration and checkpoint schema 2", "lava clock, slow band, wraps, FPS, late join and host clock handoff simulation", "rule zero reunion and multi-scout selection", "weather grace pending/loading, shared deadline, expiry, run identity, resume and clock rollover", "chasing fog movement uses tier 18 without changing saved selections", "balance and tier-20 swamp save migrations and snow off intervals", "1.5.0-1.5.16 save round trips, separate fog damage tier 14 and movement tier 18 gates, version mismatch and downgrade rejection" } };
string reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(reportJson);
if (args.Length > 0) File.WriteAllText(args[0], reportJson);
