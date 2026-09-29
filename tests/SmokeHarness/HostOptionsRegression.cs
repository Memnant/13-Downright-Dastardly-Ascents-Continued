using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

internal static class HostOptionsRegression
{
    private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static bool master, throwOnSpawn, interceptDamage;
    private static bool roomSpawn;
    private static GameObject spawned;
    private static string prefab;
    private static Vector3 spawnPosition;
    private static int spawnCalls, kinematicCalls;
    private static double clock;
    private static float damage;
    private static bool ignoresInvincibility;

    internal static int Run(Assembly mod)
    {
        var coordinator = mod.GetType("dda.RunCoordinator");
        var active = coordinator.GetField("Active", Flags); var running = coordinator.GetField("InRun", Flags);
        var resume = coordinator.GetField("ResumePrepared", Flags);
        object oldActive = active.GetValue(null), oldRunning = running.GetValue(null), oldResume = resume.GetValue(null);
        var protection = mod.GetType("dda.OpeningProtection"); var stateField = protection.GetField("state", Flags);
        object oldState = stateField.GetValue(null);
        var honor = mod.GetType("dda.SummitHonor");
        var characterType = AccessTools.TypeByName("Character"); var local = characterType.GetField("localCharacter", Flags);
        object oldLocal = local.GetValue(null);
        var characters = (IList)characterType.GetField("AllCharacters", Flags).GetValue(null);
        var root = new GameObject("Continued host options fixture"); root.SetActive(false);
        var second = new GameObject("Continued migrated statue fixture"); second.SetActive(false);
        var result = new GameObject("Continued native spawn result fixture"); result.SetActive(false);
        var player = new GameObject("Continued opening protection player"); player.SetActive(false);
        var patches = new Harmony("dda.continued.host-options-fixture");
        Component character = null;
        int checks = 0;
        try
        {
            running.SetValue(null, true); resume.SetValue(null, false);
            var photon = AccessTools.TypeByName("Photon.Pun.PhotonNetwork");
            patches.Patch(AccessTools.PropertyGetter(photon, "IsMasterClient"), prefix: new HarmonyMethod(typeof(HostOptionsRegression), "Master"));
            var instantiate = photon.GetMethods(Flags).Single(m => m.Name == "Instantiate" && m.GetParameters().Length == 5);
            patches.Patch(instantiate, prefix: new HarmonyMethod(typeof(HostOptionsRegression), "Instantiate"));
            patches.Patch(AccessTools.Method(photon, "InstantiateRoomObject"), prefix: new HarmonyMethod(typeof(HostOptionsRegression), "Instantiate"));
            var itemType = AccessTools.TypeByName("Item"); result.AddComponent(itemType); spawned = result;
            patches.Patch(AccessTools.Method(itemType, "SetKinematicNetworked", new[] { typeof(bool) }), prefix: new HarmonyMethod(typeof(HostOptionsRegression), "Kinematic"));
            var viewType = AccessTools.TypeByName("Photon.Pun.PhotonView"); root.AddComponent(viewType); second.AddComponent(viewType);
            var statueType = AccessTools.TypeByName("Peak.ScoutStatue"); var statue = root.AddComponent(statueType);
            var migrated = second.AddComponent(statueType);
            var nativeGem = Resources.Load<GameObject>("0_Items/Strange Gem");
            var nativeHonor = Resources.Load<GameObject>("0_Items/ScoutsHonor");
            Assert(nativeGem != null && nativeHonor != null, "both native summit item resources load");
            var warpType = AccessTools.TypeByName("Peak.Action_WarpToShadowRealm");
            var warp = nativeHonor.GetComponent(warpType);
            Assert(warp != null && Convert.ToInt32(warpType.GetField("segmentToWarpTo", Flags).GetValue(warp)) == 6,
                "native honor retains Nadir action");
            foreach (var instance in new[] { statue, migrated })
            {
                Set(instance, "gemPrefab", nativeGem); Set(instance, "scoutsHonorPrefab", nativeHonor);
                Set(instance, "gemSpot", root.transform); Set(instance, "amuletObjects", new GameObject[4]);
            }
            root.transform.position = new Vector3(1, 2, 3);
            var spawn = statueType.GetMethod("SpawnGem_Master", Flags);
            foreach (int level in new[] { 0, 9, 12, 19, 20 })
            foreach (bool free in new[] { false, true })
            {
                SetRules(level, free); master = true; honor.GetMethod("Reset", Flags).Invoke(null, null);
                Set(statue, "spawnedGem_master", null); spawnCalls = kinematicCalls = 0;
                spawn.Invoke(statue, null);
                bool enabled = level >= 9 && free;
                Assert(spawnCalls == 1 && prefab == "0_Items/" + (enabled ? nativeHonor.name : nativeGem.name),
                    "native spawn selects correct prefab at " + level + " / " + free);
                Assert(spawnPosition == root.transform.position && kinematicCalls == 1, "native position and kinematic setup retained");
                Assert(roomSpawn == enabled, "gift uses room ownership; original mode keeps its native spawn route");
                Assert(ReferenceEquals(Get(statue, "gemPrefab"), nativeGem), "native prefab reference remains unchanged");
                spawn.Invoke(statue, null); Assert(spawnCalls == 1, "existing statue item is not duplicated");
            }
            SetRules(20, true); master = false; honor.GetMethod("Reset", Flags).Invoke(null, null);
            Set(statue, "spawnedGem_master", null); spawnCalls = 0; spawn.Invoke(statue, null);
            Assert(spawnCalls == 0, "joining client does not generate honor");
            master = true; spawn.Invoke(statue, null); Assert(spawnCalls == 1, "host generates one honor");
            Set(migrated, "spawnedGem_master", null); spawn.Invoke(migrated, null);
            Assert(spawnCalls == 1, "promoted host with native field unset cannot duplicate issued honor in ledger simulation");
            Set(statue, "spawnedGem_master", null); spawn.Invoke(statue, null);
            Assert(spawnCalls == 1, "taken/destroyed honor does not replenish on repeated initialization");
            foreach (int type in new[] { 0, 1, 2, 3, 99 }) statueType.GetMethod("RPC_InsertAmulet", Flags).Invoke(statue, new object[] { type });
            Assert(((int[])Get(statue, "hasAmulets")).All(x => x == -1), "direct honor does not fake amulet collection");
            statueType.GetMethod("Interact_CastFinished", Flags).Invoke(statue, new object[] { null });
            Assert(spawnCalls == 1, "stale interaction does not consume items or spawn another honor");
            honor.GetMethod("Reset", Flags).Invoke(null, null); throwOnSpawn = true;
            bool failed = false;
            try { spawn.Invoke(statue, null); } catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { failed = true; }
            throwOnSpawn = false;
            Assert(failed && ReferenceEquals(Get(statue, "gemPrefab"), nativeGem), "spawn exception cannot leave a mutated prefab reference");

            // Keep real invincibility and AddStatus gates; substitute the clock and final
            // unprotected damage delivery only. No room, RPC transport, or actual player is used.
            patches.Patch(AccessTools.PropertyGetter(mod.GetType("dda.TideSync"), "Clock"), prefix: new HarmonyMethod(typeof(HostOptionsRegression), "Clock"));
            player.AddComponent(viewType);
            character = player.AddComponent(characterType); local.SetValue(null, character);
            var dataType = AccessTools.TypeByName("CharacterData"); var data = player.AddComponent(dataType);
            Set(character, "data", data); Set(data, "character", character);
            var afflictionsType = AccessTools.TypeByName("CharacterAfflictions"); var afflictions = player.AddComponent(afflictionsType);
            Set(afflictions, "character", character); afflictionsType.GetMethod("InitStatusArrays", Flags).Invoke(afflictions, null);
            var refsField = characterType.GetField("refs", Flags); var references = Activator.CreateInstance(refsField.FieldType);
            refsField.FieldType.GetField("afflictions", Flags).SetValue(references, afflictions); refsField.SetValue(character, references);
            characters.Add(character);
            var recalculate = dataType.GetMethod("RecalculateInvincibility", Flags);
            var tick = protection.GetMethod("Tick", Flags); var reset = protection.GetMethod("Reset", Flags);
            var stateType = mod.GetType("dda.OpeningProtectionState");
            var state = Activator.CreateInstance(stateType); Set(state, "generation", Guid.NewGuid().ToString("N"));
            Set(state, "clock", 0d); Set(state, "started", false);
            SetRules(20, true); clock = 100; stateField.SetValue(null, state);
            var start = protection.GetMethod("Start", Flags); start.Invoke(null, null);
            Assert((bool)Get(state, "started") && (double)Get(state, "clock") == 100d, "native run-start hook starts the armed window");
            clock = 105; start.Invoke(null, null);
            Assert((double)Get(state, "clock") == 100d && (double)protection.GetProperty("Remaining", Flags).GetValue(null) == 25d,
                "repeated start callback does not restart protection");
            clock = 100; tick.Invoke(null, null);
            Assert((bool)Get(data, "isInvincible"), "new run protection reaches native invincibility flag");
            var addStatus = afflictionsType.GetMethod("AddStatus", Flags);
            var injury = Enum.Parse(addStatus.GetParameters()[0].ParameterType, "Injury");
            Assert(!(bool)addStatus.Invoke(afflictions, new object[] { injury, .4f, false, false, false, false, false }),
                "native injury gate blocks damage instead of ascent 12's 25 percent leakage");
            patches.Patch(addStatus, prefix: new HarmonyMethod(typeof(HostOptionsRegression), "Damage") { priority = Priority.Last });
            interceptDamage = true;
            addStatus.Invoke(afflictions, new object[] { injury, .4f, false, false, false, false, false });
            Assert(!ignoresInvincibility && Math.Abs(damage - .4f) < .00001f, "opening protection bypasses only the old invincibility nerf");
            clock = 129.9; tick.Invoke(null, null); Assert((bool)Get(data, "isInvincible"), "still protected before deadline");
            clock = 130; tick.Invoke(null, null); Assert(!(bool)Get(data, "isInvincible"), "protection expires at shared deadline");
            SetRules(19, true); clock = 100; tick.Invoke(null, null); recalculate.Invoke(data, null);
            Assert(!(bool)Get(data, "isInvincible"), "ascent 19 does not receive opening protection");
            SetRules(20, true); resume.SetValue(null, true); tick.Invoke(null, null); recalculate.Invoke(data, null);
            Assert(!(bool)Get(data, "isInvincible"), "native resume gets no new protection");
            resume.SetValue(null, false); Set(character, "isBot", true); tick.Invoke(null, null);
            Assert(!(bool)Get(data, "isInvincible"), "bots are excluded"); Set(character, "isBot", false);
            var invincibilityType = AccessTools.TypeByName("Peak.Afflictions.Affliction_Invincibility");
            var milk = Activator.CreateInstance(invincibilityType); Set(milk, "totalTime", 90f); Set(milk, "timeElapsed", 10f); Set(milk, "isFromMilk", true);
            var list = (IList)Get(afflictions, "afflictionList"); list.Add(milk);
            tick.Invoke(null, null); clock = 130; tick.Invoke(null, null);
            Assert((bool)Get(data, "isInvincible") && (bool)Get(data, "isInvincibleMilk"), "existing milk protection remains after opening expires");
            Assert((float)Get(milk, "totalTime") == 90f && (float)Get(milk, "timeElapsed") == 10f, "native item protection timer is not overwritten");
            addStatus.Invoke(afflictions, new object[] { injury, .4f, false, false, false, false, false });
            Assert(ignoresInvincibility && Math.Abs(damage - .1f) < .00001f, "normal ascent 12 invincibility nerf resumes after opening window");
            list.Clear(); clock = 100; tick.Invoke(null, null); reset.Invoke(null, null);
            Assert(!(bool)Get(data, "isInvincible"), "return/reset removes only opening protection");
            recalculate.Invoke(data, null); Assert(!(bool)Get(data, "isInvincible"), "protection does not leak into later runs");
            return checks;
        }
        finally
        {
            throwOnSpawn = interceptDamage = false;
            protection.GetMethod("Reset", Flags).Invoke(null, null);
            honor.GetMethod("Reset", Flags).Invoke(null, null);
            if (character != null) characters.Remove(character);
            stateField.SetValue(null, oldState); active.SetValue(null, oldActive); running.SetValue(null, oldRunning); resume.SetValue(null, oldResume); local.SetValue(null, oldLocal);
            patches.UnpatchSelf(); spawned = null;
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(second); UnityEngine.Object.DestroyImmediate(result); UnityEngine.Object.DestroyImmediate(player);
        }
        void SetRules(int level, bool free) => active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), level, 0, true, true, free));
        void Assert(bool pass, string message) { if (!pass) throw new Exception(message); checks++; }
    }
    private static object Get(object obj, string name) => AccessTools.Field(obj.GetType(), name).GetValue(obj);
    private static void Set(object obj, string name, object value) => AccessTools.Field(obj.GetType(), name).SetValue(obj, value);
    private static bool Master(ref bool __result) { __result = master; return false; }
    private static bool Instantiate(object[] __args, MethodBase __originalMethod, ref GameObject __result)
    {
        if (throwOnSpawn) throw new InvalidOperationException("Expected spawn fixture exception");
        prefab = (string)__args[0]; spawnPosition = (Vector3)__args[1]; roomSpawn = __originalMethod.Name == "InstantiateRoomObject";
        spawnCalls++; __result = spawned; return false;
    }
    private static bool Kinematic(object[] __args) { if ((bool)__args[0]) kinematicCalls++; return false; }
    private static bool Clock(ref double __result) { __result = clock; return false; }
    private static bool Damage(float amount, bool ignoreInvincibility)
    { if (!interceptDamage) return true; damage = amount; ignoresInvincibility = ignoreInvincibility; return false; }
}
