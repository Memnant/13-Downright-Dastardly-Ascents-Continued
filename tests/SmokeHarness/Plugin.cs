using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using BepInEx;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("dda.continued.smoke-harness", "Continued isolated load verifier", "1.0.0")]
[BepInDependency("13dastardlyascents")]
public sealed class SmokeHarness : BaseUnityPlugin
{
    [DataContract] public sealed class MemberEntry { [DataMember] public string type = null; [DataMember] public string name = null; }
    [DataContract] public sealed class MemberList { [DataMember] public MemberEntry[] members = null; }
    private void Awake()
    {
        bool success = false;
        try
        {
            var plugin = BepInEx.Bootstrap.Chainloader.PluginInfos["13dastardlyascents"].Instance;
            bool ready = (bool)plugin.GetType().GetField("Ready", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var targets = Harmony.GetAllPatchedMethods().Where(m => Harmony.GetPatchInfo(m).Owners.Contains("13dastardlyascents")).Select(m => m.DeclaringType.FullName + "." + m.Name).OrderBy(m => m).ToArray();
            var expected = plugin.GetType().Assembly.GetTypes().SelectMany(t => t.GetCustomAttributes(typeof(HarmonyPatch), false).Cast<HarmonyPatch>())
                .Select(p => p.info.declaringType.FullName + "." + p.info.methodName).Distinct().ToArray();
            var missing = expected.Except(targets).ToArray();
            int numericAssertions = CheckTemporaryStats(plugin.GetType().Assembly);
            int trackedAssertions = CheckTrackedStats(plugin.GetType().Assembly);
            int dragAssertions = CheckNativeSharedDrag(plugin.GetType().Assembly);
            int regressionAssertions = CheckRecoveryAndRelease(plugin.GetType().Assembly);
            int surfaceAssertions = SurfaceRegression.Run(plugin.GetType().Assembly);
            int citadelAssertions = CitadelRegression.Run(plugin.GetType().Assembly);
            int weatherAssertions = WeatherRegression.Run(plugin.GetType().Assembly);
            int regionalRecoveryAssertions = RegionalRecoveryRegression.Run(plugin.GetType().Assembly);
            int everyMapSnowAssertions = EveryMapSnowRegression.Run(plugin.GetType().Assembly);
            int tornadoBoundaryAssertions = TornadoBoundaryRegression.Run(plugin.GetType().Assembly);
            int gloomGrowthAssertions = GloomGrowthRegression.Run(plugin.GetType().Assembly);
            int hostOptionsAssertions = HostOptionsRegression.Run(plugin.GetType().Assembly);
            int weatherGraceAssertions = WeatherGraceRegression.Run(plugin.GetType().Assembly);
            int tornadoRecoveryAssertions = TornadoRecoveryRegression.Run(plugin.GetType().Assembly);
            int chasingFogAssertions = ChasingFogRegression.Run(plugin.GetType().Assembly);
            int swampSnowAssertions = SwampSnowRegression.Run(plugin.GetType().Assembly);
            int fogStatusAssertions = FogStatusRegression.Run(plugin.GetType().Assembly);
            int swampFogAssertions = SwampFogRegression.Run(plugin.GetType().Assembly);
            success = ready && expected.Length > 0 && missing.Length == 0 && numericAssertions >= 90;
            File.WriteAllText(Path.Combine(Paths.BepInExRootPath, "smoke-result.txt"),
                (success ? "PASSED_LOAD" : "FAILED_LOAD") + "\nPluginReady=" + ready + "\nDistinctTargets=" + targets.Length + "\nMissingTargets=" + string.Join(",", missing) + "\nTemporaryStatsAssertions=" + numericAssertions + "\nTrackedStatsAssertions=" + trackedAssertions + "\nSyntheticNativeDragAssertions=" + dragAssertions + "\nRecoveryAndReleaseAssertions=" + regressionAssertions + "\nNativeSurfaceAndItemAssertions=" + surfaceAssertions + "\nCitadelParameterAndReadinessAssertions=" + citadelAssertions + "\n" + string.Join("\n", targets));
            File.AppendAllText(Path.Combine(Paths.BepInExRootPath, "smoke-result.txt"), "\nNativeWeatherAssertions=" + weatherAssertions);
            File.AppendAllText(Path.Combine(Paths.BepInExRootPath, "smoke-result.txt"), "\nNativeRegionalRecoveryAssertions=" + regionalRecoveryAssertions);
            File.AppendAllText(Path.Combine(Paths.BepInExRootPath, "smoke-result.txt"), "\nEveryMapSnowAssertions=" + everyMapSnowAssertions);
            File.AppendAllText(Path.Combine(Paths.BepInExRootPath, "smoke-result.txt"), "\nTornadoBoundaryAssertions=" + tornadoBoundaryAssertions);
            File.AppendAllText(Path.Combine(Paths.BepInExRootPath, "smoke-result.txt"), "\nGloomGrowthAssertions=" + gloomGrowthAssertions);
            File.AppendAllText(Path.Combine(Paths.BepInExRootPath, "smoke-result.txt"), "\nHostOptionsAssertions=" + hostOptionsAssertions);
            File.AppendAllText(Path.Combine(Paths.BepInExRootPath, "smoke-result.txt"), "\nWeatherGraceAssertions=" + weatherGraceAssertions);
            File.AppendAllText(Path.Combine(Paths.BepInExRootPath, "smoke-result.txt"), "\nTornadoRecoveryAssertions=" + tornadoRecoveryAssertions);
            File.AppendAllText(Path.Combine(Paths.BepInExRootPath, "smoke-result.txt"), "\nChasingFogAssertions=" + chasingFogAssertions);
            File.AppendAllText(Path.Combine(Paths.BepInExRootPath, "smoke-result.txt"), "\nSwampSnowAssertions=" + swampSnowAssertions);
            File.AppendAllText(Path.Combine(Paths.BepInExRootPath, "smoke-result.txt"), "\nFogStatusAssertions=" + fogStatusAssertions);
            File.AppendAllText(Path.Combine(Paths.BepInExRootPath, "smoke-result.txt"), "\nSwampFogAssertions=" + swampFogAssertions);
            Logger.LogInfo("SMOKE_RESULT=" + success + "; this is a load test only, no gameplay or multiplayer assertions.");
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(Paths.BepInExRootPath, "smoke-result.txt"), "FAILED_LOAD\n" + error);
            Logger.LogError(error);
        }
        Application.Quit(success ? 0 : 3);
    }

    private static int CheckTemporaryStats(Assembly mod)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        var coordinator = mod.GetType("dda.RunCoordinator");
        var activeField = coordinator.GetField("Active", flags);
        var inRunField = coordinator.GetField("InRun", flags);
        var oldActive = activeField.GetValue(null);
        var oldRunning = inRunField.GetValue(null);
        int assertions = 0;
        try
        {
            // Only managed fields are exercised; no players, physics or run/save data are created.
            activeField.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), 12, 0, true, true));
            inRunField.SetValue(null, true);
            var game = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "Assembly-CSharp");
            Verify("dda.lessRope", "RopeShooter", new[] { "length" }, new[] { 100f }, new[] { 75f });
            Verify("dda.uberBeetle", "Beetle", new[] { "bonkForce", "bonkRange", "ragdollTime" }, new[] { 3f, 4f, 1f }, new[] { 30f, 6f, 10f });
            Verify("dda.worseMedkit", "Action_ModifyStatus", new[] { "changeAmount" }, new[] { 2f }, new[] { 0.42f });
            Verify("dda.ContinuedGlider", "Glider", new[] { "stamUse", "extraOpeningCost", "extraYDrag", "soarForwardForce" },
                new[] { 0.1f, 0.05f, 0.8f, 10f }, new[] { 0.15f, 0.05f, 0.8f, 10f });
            Verify("dda.uberSplode+uberEmitter", "StatusEmitter", new[] { "amount" }, new[] { 0.1f }, new[] { 0.4f });
            Verify("dda.uberSplode+uberEmitter", "StatusEmitter", new[] { "amount" }, new[] { -0.1f }, new[] { -0.1f });
            return assertions;

            void Verify(string patchName, string gameType, string[] names, float[] baseline, float[] changed)
            {
                var targetType = game.GetType(gameType);
                var target = FormatterServices.GetUninitializedObject(targetType);
                var fields = names.Select(n => targetType.GetField(n, flags)).ToArray();
                for (int i = 0; i < fields.Length; i++) fields[i].SetValue(target, baseline[i]);
                var patchType = mod.GetType(patchName);
                var prefix = patchType.GetMethod("Prefix", flags);
                var finalizer = patchType.GetMethod("Finalizer", flags);
                for (int repeat = 0; repeat < 10; repeat++)
                {
                    object[] parameters = { target, null };
                    prefix.Invoke(null, parameters);
                    for (int i = 0; i < fields.Length; i++) Assert((float)fields[i].GetValue(target), changed[i]);
                    finalizer.Invoke(null, new[] { target, parameters[1], null });
                    for (int i = 0; i < fields.Length; i++) Assert((float)fields[i].GetValue(target), baseline[i]);
                }
                inRunField.SetValue(null, false);
                object[] off = { target, null };
                prefix.Invoke(null, off);
                for (int i = 0; i < fields.Length; i++) Assert((float)fields[i].GetValue(target), baseline[i]);
                finalizer.Invoke(null, new[] { target, off[1], null });
                inRunField.SetValue(null, true);
            }
            void Assert(float value, float expected)
            {
                if (Math.Abs(value - expected) > 0.00001f) throw new Exception("Temporary stat mismatch: " + value + " expected " + expected);
                assertions++;
            }
        }
        finally { activeField.SetValue(null, oldActive); inRunField.SetValue(null, oldRunning); }
    }

    private static int CheckTrackedStats(Assembly mod)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var state = mod.GetType("dda.EffectState");
        var set = state.GetMethod("Set", flags);
        var reset = state.GetMethod("Reset", flags);
        MemberList list;
        using (var stream = File.OpenRead(Path.Combine(Paths.BepInExRootPath, "override-members.json")))
            list = (MemberList)new DataContractJsonSerializer(typeof(MemberList)).ReadObject(stream);
        int checks = 0;
        foreach (var entry in list.members)
        {
            var type = AccessTools.TypeByName(entry.type);
            if (type == null) throw new Exception("Missing override type " + entry.type);
            var field = AccessTools.Field(type, entry.name);
            if (field == null)
            {
                var property = AccessTools.Property(type, entry.name);
                if (property == null || !property.CanRead || !property.CanWrite) throw new Exception("Missing override property " + entry.type + "." + entry.name);
                checks++;
                continue; // Native component properties are resolved, not simulated on uninitialized objects.
            }
            var target = FormatterServices.GetUninitializedObject(type);
            var original = field.GetValue(target);
            object changed = field.FieldType == typeof(bool) ? (object)true :
                field.FieldType == typeof(Bounds) ? (object)new Bounds(Vector3.one, Vector3.one * 3000f) :
                field.FieldType == typeof(int) ? (object)42 : (object)17.5f;
            for (int i = 0; i < 10; i++) set.Invoke(null, new[] { target, entry.name, changed });
            if (!Equals(field.GetValue(target), changed)) throw new Exception("Override failed " + entry.type + "." + entry.name);
            checks++;
            reset.Invoke(null, null);
            if (!Equals(field.GetValue(target), original)) throw new Exception("Restore failed " + entry.type + "." + entry.name);
            checks++;
            set.Invoke(null, new[] { target, entry.name, changed });
            field.SetValue(target, original); // A later edit from another mod must survive our reset.
            reset.Invoke(null, null);
            if (!Equals(field.GetValue(target), original)) throw new Exception("External edit overwritten " + entry.type + "." + entry.name);
            checks++;
        }
        var recordType = mod.GetType("dda.SavedRuleRecord");
        var record = Activator.CreateInstance(recordType);
        recordType.GetField("schema").SetValue(record, 2);
        recordType.GetField("runId").SetValue(record, "e354b651cbba4c0aaa49765432c2e50a");
        recordType.GetField("gameVersion").SetValue(record, "2.4.c");
        recordType.GetField("modVersion").SetValue(record, "1.5.1");
        recordType.GetField("rules").SetValue(record, "1:20:0:1:1");
        recordType.GetField("nativeSaveHash").SetValue(record, new string('A', 64));
        recordType.GetField("checkpointSegment").SetValue(record, 3);
        recordType.GetField("checkpointTime").SetValue(record, 350d);
        recordType.GetField("tidePhase").SetValue(record, 1.8d);
        string json = (string)recordType.GetMethod("ToJson").Invoke(record, null);
        var restored = recordType.GetMethod("FromJson").Invoke(null, new object[] { json });
        if ((string)recordType.GetField("rules").GetValue(restored) != "1:20:0:1:1") throw new Exception("Mono sidecar JSON round trip failed.");
        object[] readArgs = { Guid.ParseExact("e354b651cbba4c0aaa49765432c2e50a", "N"), "2.4.c", "1.5.1", null };
        if (!(bool)recordType.GetMethod("TryRead").Invoke(restored, readArgs)) throw new Exception("Mono schema 2 sidecar validation failed.");
        // Match the subset parser against the native JSON shape, including ignored engine data.
        var nativeType = mod.GetType("dda.RunSaveStore").GetNestedType("NativeFile", BindingFlags.NonPublic);
        var decode = mod.GetType("dda.WireJson").GetMethod("Decode").MakeGenericMethod(nativeType);
        var native = decode.Invoke(null, new object[] { "{\"version\":3,\"run\":{\"runId\":\"e354b651-cbba-4c0a-aa49-765432c2e50a\",\"runTimer\":123.25,\"biomeReached\":3},\"playerSaves\":[]}" });
        var nativeRun = nativeType.GetField("run").GetValue(native);
        if ((float)nativeRun.GetType().GetField("runTimer").GetValue(nativeRun) != 123.25f) throw new Exception("Native checkpoint subset JSON parser failed.");
        return checks + 3;
    }

    private static int CheckNativeSharedDrag(Assembly mod)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var coordinator = mod.GetType("dda.RunCoordinator");
        var active = coordinator.GetField("Active", flags);
        var inRun = coordinator.GetField("InRun", flags);
        object oldActive = active.GetValue(null), oldInRun = inRun.GetValue(null);
        var go = new GameObject("Continued isolated bodypart fixture");
        try
        {
            active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), 12, 0, true, true));
            inRun.SetValue(null, true);
            var rigidType = AccessTools.TypeByName("UnityEngine.Rigidbody");
            var rigid = go.AddComponent(rigidType);
            rigidType.GetProperty("useGravity").SetValue(rigid, false);
            var velocity = rigidType.GetProperty("linearVelocity");
            var bodyType = AccessTools.TypeByName("Bodypart");
            var body = go.AddComponent(bodyType);
            var drag = AccessTools.Method(bodyType, "ParasolDrag");
            int checks = 0;
            foreach (int level in new[] { 0, 12, 20 })
            {
                active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), level, 0, true, true));
                velocity.SetValue(rigid, new Vector3(5, -10, 3));
                // This is the shared native drag method outside a parasol call, as used by Glider.
                drag.Invoke(body, new object[] { 0.8f, 1f, true });
                Vector3 result = (Vector3)velocity.GetValue(rigid);
                if ((result - new Vector3(5, -8, 3)).sqrMagnitude > 0.00001f)
                    throw new Exception("Shared drag was hijacked outside a parasol call at ascent " + level + ": " + result);
                checks++;
            }
            return checks;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            active.SetValue(null, oldActive);
            inRun.SetValue(null, oldInRun);
        }
    }

    private static int CheckRecoveryAndRelease(Assembly mod)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        var coordinator = mod.GetType("dda.RunCoordinator");
        var active = coordinator.GetField("Active", flags);
        var running = coordinator.GetField("InRun", flags);
        var characterType = AccessTools.TypeByName("Character");
        var dayType = AccessTools.TypeByName("DayNightManager");
        var local = characterType.GetField("localCharacter", flags);
        var dayInstance = dayType.GetField("instance", flags);
        object oldActive = active.GetValue(null), oldRunning = running.GetValue(null), oldLocal = local.GetValue(null), oldDay = dayInstance.GetValue(null);
        var go = new GameObject("Continued inactive regression fixtures");
        go.SetActive(false); // Do not run the gameplay Awake/Start routines or spawn a player.
        var controller = mod.GetType("dda.RuleZeroController");
        int checks = 0;
        try
        {
            var view = go.AddComponent(AccessTools.TypeByName("Photon.Pun.PhotonView"));
            var character = go.AddComponent(characterType);
            characterType.GetField("view", flags).SetValue(character, view);
            local.SetValue(null, character);
            var day = go.AddComponent(dayType);
            dayInstance.SetValue(null, day);
            dayType.GetField("dayStart", flags).SetValue(day, .25f);
            dayType.GetField("dayEnd", flags).SetValue(day, .75f);
            var afflictionType = AccessTools.TypeByName("CharacterAfflictions");
            var affliction = FormatterServices.GetUninitializedObject(afflictionType);
            afflictionType.GetField("character", flags).SetValue(affliction, character);
            afflictionType.GetField("currentStatuses", flags).SetValue(affliction, new float[20]);
            var rate = afflictionType.GetField("drowsyReductionPerSecond", flags);
            var hot = afflictionType.GetField("hotReductionPerSecond", flags);
            rate.SetValue(affliction, .07f); hot.SetValue(affliction, .01f);
            running.SetValue(null, true);
            var recovery = mod.GetType("dda.uniqueAilments");
            foreach (int level in new[] { 15, 16, 17 })
            foreach (float daylight in new[] { 0f, 1f })
            {
                active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), level, 0, true, true));
                dayType.GetField("timeOfDay", flags).SetValue(day, daylight == 0 ? .1f : .5f);
                object[] parameters = { affliction, null };
                recovery.GetMethod("Prefix", flags).Invoke(null, parameters);
                float expected = level < 16 ? .07f : (daylight == 0 ? 0f : .02f);
                Assert(Math.Abs((float)rate.GetValue(affliction) - expected) < .00001f, "day/night recovery rate");
                recovery.GetMethod("Finalizer", flags).Invoke(null, new[] { affliction, parameters[1], null });
                Assert((float)rate.GetValue(affliction) == .07f, "recovery rate restored after call");
            }
            var scoutType = AccessTools.TypeByName("Scoutmaster");
            var scout = go.AddComponent(scoutType);
            var targetField = scoutType.GetField("_currentTarget", flags);
            var forcedField = scoutType.GetField("targetForcedUntil", flags);
            float deadline = Time.time + 30;
            targetField.SetValue(scout, character);
            forcedField.SetValue(scout, deadline);
            controller.GetField("ownedScout", flags).SetValue(null, scout);
            controller.GetField("ownedTarget", flags).SetValue(null, 0); // Inactive fixture view has no assigned network ID.
            controller.GetField("ownedUntil", flags).SetValue(null, deadline);
            controller.GetMethod("ReleaseLocal", flags).Invoke(null, null);
            Assert(targetField.GetValue(scout) == null, "native forced target cleared by mod-owned release");
            Assert((float)forcedField.GetValue(scout) <= Time.time, "forced timer cleared before native null setter");
            targetField.SetValue(scout, character); forcedField.SetValue(scout, deadline);
            controller.GetField("ownedScout", flags).SetValue(null, scout);
            controller.GetField("ownedTarget", flags).SetValue(null, 0);
            controller.GetField("ownedUntil", flags).SetValue(null, deadline);
            controller.GetMethod("BeforeNativeTarget", flags).Invoke(null, new object[] { scout, 60f });
            forcedField.SetValue(scout, Time.time + 60f);
            controller.GetMethod("ReleaseLocal", flags).Invoke(null, null);
            Assert(targetField.GetValue(scout) == character, "external forced summon is not canceled by old lease");
            return checks;
        }
        finally
        {
            controller.GetMethod("Reset", flags).Invoke(null, null);
            active.SetValue(null, oldActive); running.SetValue(null, oldRunning);
            local.SetValue(null, oldLocal); dayInstance.SetValue(null, oldDay);
            UnityEngine.Object.DestroyImmediate(go);
        }
        void Assert(bool pass, string scenario) { if (!pass) throw new Exception(scenario); checks++; }
    }
}
