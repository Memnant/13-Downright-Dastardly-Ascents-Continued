using dda;

internal static class TornadoRecoverySaveTests
{
    internal static void Run(Action<bool, string> check)
    {
        var id = Guid.NewGuid();
        for (int patch = 0; patch <= 9; patch++)
        foreach (bool free in new[] { false, true })
        {
            var record = new SavedRuleRecord { schema = patch == 0 ? 1 : 2, runId = id.ToString("N"), gameVersion = "2.4.c",
                modVersion = "1.5." + patch, rules = patch < 7 ? "1:20:4095:0:1" : new DifficultySnapshot(20, 4095, false, true, free).Encode(),
                nativeSaveHash = new string('A', 64), checkpointSegment = 3, checkpointTime = 350, tidePhase = 1.8 };
            var copy = SavedRuleRecord.FromJson(record.ToJson());
            check(copy.TryRead(id, "2.4.c", "1.5.9", out var rules) && rules.Ascent == 20 && rules.ForceMask == 4095 &&
                !rules.NerfedRevives && rules.CursePunishment && rules.FreeSummitHonor == (patch >= 7 && free) &&
                copy.tidePhase == 1.8 && copy.checkpointSegment == 3 && copy.nativeSaveHash == record.nativeSaveHash,
                "1.5.9 retains prior/current sidecar rules, checkpoint, tide, and summit option");
            check(!copy.TryRead(Guid.NewGuid(), "2.4.c", "1.5.9", out _), "1.5.9 upgrade retains RunId isolation");
            if (patch == 9) check(!copy.TryRead(id, "2.4.c", "1.5.8", out _), "1.5.9 downgrade requires paired backup");
        }
        var selected = new DifficultySnapshot(20, 0, true, true);
        check(ReadinessPolicy.Check(selected, "1.5.9", "1.5.9", selected.Encode()) == PeerReadiness.Ready, "same recovery version ready");
        check(ReadinessPolicy.Check(selected, "1.5.9", "1.5.8", selected.Encode()) == PeerReadiness.VersionMismatch, "older recovery behavior cannot join extended run");
    }
}
