using System;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Photon.Pun;

// Exercise the actual sidecar writer/reader with an isolated native-file fixture.
// Never invoke Quicksave.TryLoadSave or change the user's native save location.
internal static class PatchSaveRegression
{
    private static FileInfo nativeFile;
    private static Guid runId;
    internal static int Run(Assembly mod)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        var coordinator = mod.GetType("dda.RunCoordinator");
        var plugin = mod.GetType("dda.Plugin");
        var fields = new[] { coordinator.GetField("Selected", flags), coordinator.GetField("Active", flags),
            coordinator.GetField("InRun", flags), coordinator.GetField("ResumePrepared", flags),
            coordinator.GetField("Notice", flags), plugin.GetField("ActualGameVersion", flags) };
        var previous = new object[fields.Length];
        for (int i = 0; i < fields.Length; i++) previous[i] = fields[i].GetValue(null);
        var patches = new Harmony("dda.continued.patch-save-fixture");
        int checks = 0;
        runId = Guid.NewGuid();
        nativeFile = new FileInfo(Path.Combine(Paths.ConfigPath, "native-patch-fixture.peak"));
        string sidecar = Path.Combine(Paths.ConfigPath, "13dda-continued", "runs", runId.ToString("N") + ".json");
        string native = "{\"version\":4,\"run\":{\"runId\":\"" + runId.ToString("N") + "\",\"biomeReached\":3,\"runTimer\":678.5}}";
        var store = mod.GetType("dda.RunSaveStore");
        var recordType = mod.GetType("dda.SavedRuleRecord");
        var save = store.GetMethod("SaveCheckpoint", flags);
        var resume = store.GetMethod("PrepareResume", flags);
        try
        {
            Patch(AccessTools.PropertyGetter(AccessTools.TypeByName("Peak.Quicksave"), "SaveFile"), nameof(SaveFile));
            Patch(AccessTools.PropertyGetter(mod.GetType("dda.TideSync"), "RunId"), nameof(RunId));
            Patch(AccessTools.PropertyGetter(typeof(PhotonNetwork), "InRoom"), nameof(InRoom));
            fields[1].SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), 9, 0, true, true));
            fields[2].SetValue(null, true); fields[3].SetValue(null, false); fields[5].SetValue(null, "3.0.0");
            File.WriteAllText(nativeFile.FullName, native);
            save.Invoke(null, null);
            string original = File.ReadAllText(sidecar);
            object record = Read();
            Assert((string)Get(record, "gameVersion") == "3.0.0" && (int)Get(record, "nativeSaveVersion") == 4, "writer records actual game and native format");
            Assert((string)Get(record, "modVersion") == "1.5.22", "writer records this plugin version");
            fields[5].SetValue(null, "3.0.1");
            Assert(Resume(), "same-format forward patch resumes rules");
            Assert(File.ReadAllText(sidecar) == original && File.ReadAllText(nativeFile.FullName) == native, "resume reads without rewriting native or sidecar");
            Assert((int)mod.GetType("dda.DifficultySnapshot").GetProperty("Ascent").GetValue(fields[1].GetValue(null)) == 9, "resumed rules keep chosen tier");
            fields[3].SetValue(null, false);
            save.Invoke(null, null);
            string newer = File.ReadAllText(sidecar);
            record = Read();
            Assert((string)Get(record, "gameVersion") == "3.0.1", "next save records actual new patch instead of hard-coded baseline");
            fields[5].SetValue(null, "3.0.0");
            Assert(!Resume() && File.ReadAllText(sidecar) == newer, "game downgrade is rejected without rewriting sidecar");
            fields[5].SetValue(null, "3.0.1");
            File.WriteAllText(nativeFile.FullName, native.Replace("\"version\":4", "\"version\":5"));
            bool rejected = false;
            try { save.Invoke(null, null); }
            catch (TargetInvocationException error) when (error.InnerException is InvalidDataException) { rejected = true; }
            Assert(rejected && File.ReadAllText(sidecar) == newer, "unknown native format cannot overwrite checkpoint metadata");
            Assert(!Resume(), "unknown native format cannot resume extended rules");
            string changed = native.Replace("678.5", "679.5");
            File.WriteAllText(nativeFile.FullName, changed);
            Assert(!Resume() && File.ReadAllText(nativeFile.FullName) == changed, "checkpoint mismatch preserves native fixture");
            File.WriteAllText(nativeFile.FullName, native);
            recordType.GetField("modVersion").SetValue(record, "1.5.20");
            recordType.GetField("gameVersion").SetValue(record, "2.6.a");
            recordType.GetField("nativeSaveVersion").SetValue(record, null);
            File.WriteAllText(sidecar, (string)recordType.GetMethod("ToJson").Invoke(record, null));
            Assert(!Resume(), "old 2.6 sidecar cannot migrate into the new map pool automatically");
            Assert(File.ReadAllText(nativeFile.FullName) == native, "native fixture unchanged after legacy sidecar read");
        }
        finally
        {
            coordinator.GetMethod("ReturnToAirport", flags).Invoke(null, null);
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, previous[i]);
            patches.UnpatchSelf();
        }
        return checks;

        void Assert(bool value, string message) { if (!value) throw new Exception("Patch save fixture: " + message); checks++; }
        void Patch(MethodBase target, string method) => patches.Patch(target, prefix: new HarmonyMethod(typeof(PatchSaveRegression), method));
        object Read() => recordType.GetMethod("FromJson").Invoke(null, new object[] { File.ReadAllText(sidecar) });
        object Get(object record, string name) => recordType.GetField(name).GetValue(record);
        bool Resume() => (bool)resume.Invoke(null, new object[] { runId.ToString("N"), 8 });
    }
    private static bool SaveFile(ref FileInfo __result) { __result = nativeFile; return false; }
    private static bool RunId(ref Guid __result) { __result = runId; return false; }
    private static bool InRoom(ref bool __result) { __result = false; return false; }
}
