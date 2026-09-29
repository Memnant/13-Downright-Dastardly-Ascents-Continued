using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

internal static class Game25StatusRegression
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    internal static int Run(Assembly mod)
    {
        var coordinator = mod.GetType("dda.RunCoordinator");
        var active = coordinator.GetField("Active", F); var running = coordinator.GetField("InRun", F);
        var characterType = AccessTools.TypeByName("Character"); var local = characterType.GetField("localCharacter", F);
        var fields = new[] { active, running, local }; var saved = new object[fields.Length];
        for (int i = 0; i < fields.Length; i++) saved[i] = fields[i].GetValue(null);
        var root = new GameObject("Continued 2.5 status interface fixture"); root.SetActive(false);
        var patches = new Harmony("dda.continued.game25-status-fixture"); int checks = 0;
        try
        {
            var viewType = AccessTools.TypeByName("Photon.Pun.PhotonView"); var view = root.AddComponent(viewType);
            Set(view, "<IsMine>k__BackingField", true);
            var character = root.AddComponent(characterType); Set(character, "view", view); local.SetValue(null, character);
            var data = root.AddComponent(AccessTools.TypeByName("CharacterData"));
            Set(character, "data", data); Set(data, "character", character); Set(data, "isInFog", true);
            var type = AccessTools.TypeByName("CharacterAfflictions"); var afflictions = root.AddComponent(type);
            Set(afflictions, "character", character);
            var current = new float[15]; var increments = new float[15]; var decrements = new float[15];
            Set(afflictions, "currentStatuses", current); Set(afflictions, "currentIncrementalStatuses", increments);
            Set(afflictions, "currentDecrementalStatuses", decrements); Set(afflictions, "lastAddedIncrementalStatus", new float[15]);
            var refsField = characterType.GetField("refs", F); var refs = Activator.CreateInstance(refsField.FieldType);
            Set(refs, "afflictions", afflictions); refsField.SetValue(character, refs);
            var add = type.GetMethod("AddStatus", F); var setAll = type.GetMethod("SetAll", F);
            Assert(add.GetParameters().Length == 7 && setAll.GetParameters().Length == 2, "2.5 native signatures resolve");
            var injury = Enum.Parse(type.GetNestedType("STATUSTYPE"), "Injury");
            patches.Patch(AccessTools.PropertyGetter(AccessTools.TypeByName("Ascents"), "etcDamageMultiplier"),
                prefix: new HarmonyMethod(typeof(Game25StatusRegression), nameof(UnitDamage)));
            running.SetValue(null, true);
            foreach (int level in new[] { 0, 12, 17, 20 })
            foreach (bool skeleton in new[] { false, true })
            foreach (bool ignoreSkeleton in new[] { false, true })
            {
                active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), level, 0, true, true));
                Set(data, "_isSkeleton", skeleton);
                Array.Clear(current, 0, 15); Array.Clear(increments, 0, 15);
                // Below the native 0.025 batching threshold, so real status logic
                // runs without requiring a HUD, transport, or persistent save.
                bool accepted = (bool)add.Invoke(afflictions, new object[] { injury, .001f, true, false, false, false, ignoreSkeleton });
                float expected = skeleton && !ignoreSkeleton ? .008f : .001f;
                Assert(accepted && Math.Abs(increments[0] - expected) < 1e-7f, "native skeleton flag controls injury amplification exactly once");
                Array.Clear(current, 0, 15); Array.Clear(increments, 0, 15);
                var statuses = new float[15]; statuses[0] = .001f;
                setAll.Invoke(afflictions, new object[] { statuses, ignoreSkeleton });
                Assert(level == 0 ? Math.Abs(increments[0] - expected) < 1e-7f : current[0] == .001f && increments[0] == 0,
                    "native SetAll forwards the flag; continued replication restores absolute state without amplification");
                Assert(!(bool)mod.GetType("dda.StatusReplication").GetProperty("Active", F).GetValue(null), "replication scope is released");
            }
            return checks;
        }
        finally
        {
            patches.UnpatchSelf();
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, saved[i]);
            UnityEngine.Object.DestroyImmediate(root);
        }
        void Assert(bool pass, string reason) { if (!pass) throw new Exception(reason); checks++; }
    }
    private static bool UnitDamage(ref float __result) { __result = 1f; return false; }
    private static void Set(object instance, string name, object value) => AccessTools.Field(instance.GetType(), name).SetValue(instance, value);
}
