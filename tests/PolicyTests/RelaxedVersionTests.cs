using dda;

internal static class RelaxedVersionTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (string game in new[] { "3.0.0", "3.0.0--logFile", "3.0.1", "3.0.1--logFile", "3.0.10", "3.0.10--logFile", "3.1.0", "3.1.0--logFile", "3.12.4" })
            check(GameVersionPolicy.Allows(game), "allow numeric versions in the 3.x series");
        foreach (string game in new[] { null, "", "2.6", "2.6.", "2.6.A", "2.6.a beta", "2.6.a\n", " 2.6.a", "2.6.a.1", "2.60.a", "2.7.a", "3.0.a", "2.5.a", "2.6.1", "3.0", "3.0.a", "3.0.01", "3.0.-1", "3.0.2147483648", "3.2147483648.0", "3.01.0", "3.1", "3.1.0.0", "4.0.0", "3.0.0 beta", "3.0.0\n",
            "--logFile", "3.0.--logFile", "3.0.01--logFile", "3.0.1--logfile", "3.0.1 --logFile", "3.0.1--logFile\n", "3.0.1--logFile--logFile", "3.0.1--logFile.x", "4.0.0--logFile", "2.6.a--logFile" })
            check(!GameVersionPolicy.Allows(game), "other major versions and malformed versions are rejected");
        check(GameVersionPolicy.Normalize("3.0.1--logFile") == "3.0.1" && GameVersionPolicy.Normalize("3.1.0--logFile") == "3.1.0",
            "Steam packaging suffix is stripped from valid 3.x versions");
        check(GameVersionPolicy.Normalize("3.0.1--logFile--logFile") == "3.0.1--logFile--logFile" && GameVersionPolicy.Normalize("4.0.0--logFile") == "4.0.0--logFile",
            "only one exact suffix on an allowed version is stripped");
        check(GameVersionPolicy.CanReadSave("3.0.9", "3.1.0") && GameVersionPolicy.CanReadSave("3.9.0", "3.10.0") &&
            !GameVersionPolicy.CanReadSave("3.1.0", "3.0.9") && !GameVersionPolicy.CanReadSave("3.0.1", "4.0.0"),
            "3.x saves move forward across minor versions numerically, never backwards or to 4.x");
        var id = Guid.NewGuid();
        foreach (int level in new[] { 0, 9, 14, 18, 20 })
        foreach (int force in new[] { 0, 1 << 5, 1 << 11 })
        foreach (bool honor in new[] { false, true })
        foreach (string game in new[] { "2.6.a", "2.6.b" })
        {
            var rules = new DifficultySnapshot(level, force, false, true, honor);
            var record = new SavedRuleRecord { schema = 2, runId = id.ToString("N"), gameVersion = game,
                modVersion = "1.5.21", rules = rules.Encode(), nativeSaveHash = new string('A', 64),
                checkpointSegment = 3, checkpointTime = 500, tidePhase = 1.25, nativeSaveVersion = 4 };
            string json = record.ToJson();
            var copy = SavedRuleRecord.FromJson(json);
            check(copy.TryRead(id, game, "1.5.21", out var loaded) && loaded.Encode() == rules.Encode(), "same patch save roundtrip");
            check(copy.TryRead(id, "2.6.c", "1.5.21", out loaded) && loaded.Encode() == rules.Encode(), "forward patch preserves rules");
            check(copy.ToJson() == json, "reading a patch upgrade does not rewrite metadata");
            check(!copy.TryRead(Guid.NewGuid(), game, "1.5.21", out _), "patch relaxation keeps RunId isolation");
            check(!copy.TryRead(id, "2.7.a", "1.5.21", out _), "save cannot cross unsupported series");
            check(!copy.TryRead(id, "2.6.a", "1.5.20", out _), "new sidecars cannot downgrade to old mod");
            if (game == "2.6.b") check(!copy.TryRead(id, "2.6.a", "1.5.21", out _), "game patch downgrade is rejected");
            foreach (int? format in new int?[] { null, 0, 3, 5 })
            {
                copy.nativeSaveVersion = format;
                check(!copy.TryRead(id, game, "1.5.21", out _), "unknown native format is rejected");
            }
            copy.nativeSaveVersion = 4;
            copy.nativeSaveHash = "bad";
            check(!copy.TryRead(id, game, "1.5.21", out _), "relaxation retains checkpoint integrity validation");
            copy = SavedRuleRecord.FromJson(json);
            copy.modVersion = "1.5.20"; copy.gameVersion = "2.6.a"; copy.nativeSaveVersion = null;
            check(copy.TryRead(id, game, "1.5.21", out loaded) && loaded.Encode() == rules.Encode(), "known 1.5.20 format-4 saves upgrade");
            copy.gameVersion = "2.5.a";
            check(!copy.TryRead(id, game, "1.5.21", out _), "2.5 saves are never inferred as native format 4");
            copy.gameVersion = "2.6.b";
            check(!copy.TryRead(id, game, "1.5.21", out _), "unaudited old-mod patch combinations are rejected");
        }
        var selected = new DifficultySnapshot(20, 0, true, true);
        check(ReadinessPolicy.Check(selected, "1.5.21", "1.5.20", selected.Encode()) == PeerReadiness.VersionMismatch,
            "all players still need matching mod versions");
    }
}
