using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

internal static class ChasingFogRegression
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static bool fogOn = true, failMove, failWait;
    private static int nativeAscent = 8, moves, waits;
    private static float observedSpeed;

    internal static int Run(Assembly mod)
    {
        var coordinator = mod.GetType("dda.RunCoordinator");
        var active = coordinator.GetField("Active", Flags); var running = coordinator.GetField("InRun", Flags);
        var mapType = AccessTools.TypeByName("MapHandler"); var fogType = AccessTools.TypeByName("OrbFogHandler");
        var mapInstance = mapType.BaseType.GetField("_instance", Flags); var fogInstance = fogType.BaseType.GetField("_instance", Flags);
        var characterType = AccessTools.TypeByName("Character"); var local = characterType.GetField("localCharacter", Flags);
        FieldInfo[] saved = { active, running, mapInstance, fogInstance, local };
        object[] before = new object[saved.Length];
        for (int i = 0; i < saved.Length; i++) before[i] = saved[i].GetValue(null);
        var root = new GameObject("Continued chasing fog fixture"); root.SetActive(false);
        var sphereRoot = new GameObject("Fog sphere fixture"); sphereRoot.transform.SetParent(root.transform);
        var patches = new Harmony("dda.continued.chasing-fog-fixture");
        int checks = 0;
        try
        {
            var map = root.AddComponent(mapType); mapInstance.SetValue(null, map); local.SetValue(null, null);
            var fog = root.AddComponent(fogType); fogInstance.SetValue(null, fog);
            var sphereType = AccessTools.TypeByName("FogSphere"); var sphere = sphereRoot.AddComponent(sphereType); Set(fog, "sphere", sphere);
            var segmentType = mapType.GetNestedType("MapSegment"); var segments = Array.CreateInstance(segmentType, 5);
            for (int i = 0; i < 5; i++) segments.SetValue(Activator.CreateInstance(segmentType), i);
            Set(map, "segments", segments);
            var biome = segmentType.GetField("_biome", Flags);
            var originType = AccessTools.TypeByName("FogSphereOrigin"); var origins = Array.CreateInstance(originType, 4);
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Fog origin " + i); go.transform.SetParent(root.transform);
                var origin = go.AddComponent(originType); origins.SetValue(origin, i);
                go.transform.position = new Vector3(i * 10, 200 + i * 100, i * 300);
                Set(origin, "size", i == 3 ? 10000f : 650f + 50 * i);
                Set(origin, "moveOnHeight", i == 3 ? 10000f : 70f); Set(origin, "moveOnForward", i == 3 ? 10000f : 200f);
                Set(origin, "disableFog", i == 3);
            }
            Set(fog, "origins", origins); Set(fog, "speed", .4f); Set(fog, "maxWaitTime", 1000f);
            var move = fogType.GetMethod("Move", Flags); var update = fogType.GetMethod("Update", Flags);
            var wait = fogType.GetMethod("WaitToMove", Flags); var time = fogType.GetMethod("TimeToMove", Flags);
            var setOrigin = fogType.GetMethod("SetFogOrigin", Flags); var sync = fogType.GetMethod("RPCA_SyncFog", Flags);
            var ascents = AccessTools.TypeByName("Ascents");
            patches.Patch(AccessTools.PropertyGetter(ascents, "currentAscent"), prefix: new HarmonyMethod(typeof(ChasingFogRegression), "Ascent"));
            patches.Patch(AccessTools.PropertyGetter(ascents, "fogEnabled"), prefix: new HarmonyMethod(typeof(ChasingFogRegression), "FogEnabled"));
            patches.Patch(AccessTools.PropertyGetter(AccessTools.TypeByName("Photon.Pun.PhotonNetwork"), "IsMasterClient"), prefix: new HarmonyMethod(typeof(ChasingFogRegression), "NotMaster"));
            patches.Patch(move, prefix: new HarmonyMethod(typeof(ChasingFogRegression), "ObserveMove") { priority = Priority.Last });
            // The fixture has no Photon session. Replace only the host/RPC waiting
            // routine with a call probe; native TimeToMove, Update and Move still run.
            patches.Patch(wait, prefix: new HarmonyMethod(typeof(ChasingFogRegression), "ObserveWait") { priority = Priority.Last });
            running.SetValue(null, true);

            foreach (int baseAscent in new[] { 7, 8 })
            foreach (int level in new[] { 0, 9, 17, 18, 19, 20, -9, -17, -18, -20 })
            {
                nativeAscent = baseAscent; SetRules(level);
                bool enhanced = level >= 18 || level == -18;
                for (int id = 0; id < 3; id++)
                {
                    Set(map, "currentSegment", id); setOrigin.Invoke(fog, new object[] { id });
                    Set(fog, "currentWaitTime", 20f); Assert(!(bool)time.Invoke(fog, null), "twenty-second boundary is strict");
                    Set(fog, "currentWaitTime", 20.01f); Assert((bool)time.Invoke(fog, null) == enhanced, "native official or tier-18 waiting threshold");
                    float initial = (float)Get(fog, "currentSize"); float speed = enhanced ? .8f : .4f;
                    move.Invoke(fog, null);
                    Assert(observedSpeed == speed && (float)Get(fog, "speed") == .4f, "effective speed and per-call restoration");
                    Assert(Math.Abs((float)Get(fog, "currentSize") - (initial - speed * Time.deltaTime)) < .001f, "native radius movement uses selected speed");
                }
            }
            foreach (int level in new[] { 9, 17, -9, 18, -18, 20 })
            {
                SetRules(level); Set(map, "currentSegment", 3);
                biome.SetValue(segments.GetValue(3), Enum.Parse(biome.FieldType, "Volcano"));
                setOrigin.Invoke(fog, new object[] { 3 }); bool enhanced = level >= 18 || level == -18;
                Assert((float)Get(fog, "currentSize") == (enhanced ? 790f : 10000f), "Volcano extension follows selected/forced 18, not 9");
                moves = 0; Set(fog, "isMoving", true); update.Invoke(fog, null);
                Assert(moves == (enhanced ? 1 : 0), "Volcano movement uses the same tier-18 gate");
            }
            SetRules(18); Set(map, "currentSegment", 0);
            for (int i = 0; i < 4; i++) { setOrigin.Invoke(fog, new object[] { 0 }); move.Invoke(fog, null); }
            Assert(observedSpeed == .8f && (float)Get(fog, "speed") == .4f, "repeated initialization does not compound speed");
            Set(fog, "speed", .6f); move.Invoke(fog, null);
            Assert(observedSpeed == .6f && (float)Get(fog, "speed") == .6f, "legacy guard preserves already-faster parameters");
            Set(fog, "speed", .4f); fogOn = false; move.Invoke(fog, null); Set(fog, "currentWaitTime", 21f);
            Assert(observedSpeed == .4f && !(bool)time.Invoke(fog, null), "custom fog-disabled setting is retained"); fogOn = true;
            failMove = true; Throws(() => move.Invoke(fog, null)); failMove = false;
            Assert((float)Get(fog, "speed") == .4f, "native movement exception restores speed");

            foreach (string biomeName in new[] { "Volcano", "Swamp", "Temple" })
            {
                Set(map, "currentSegment", 3); biome.SetValue(segments.GetValue(3), Enum.Parse(biome.FieldType, biomeName));
                setOrigin.Invoke(fog, new object[] { 3 }); bool volcano = biomeName == "Volcano";
                Assert((float)Get(fog, "currentSize") == (volcano ? 790f : 10000f), "extension geometry is Volcano-only");
                if (volcano) Assert((Vector3)Get(sphere, "fogPoint") == new Vector3(-3.73f, 862.99f, 1960.66f) &&
                    (float)Get(fog, "currentStartHeight") == 0 && (float)Get(fog, "currentStartForward") == 0, "original Caldera origin and progress trigger");
                Set(fog, "isMoving", true); moves = 0; update.Invoke(fog, null);
                Assert(moves == (volcano ? 1 : 0) && (bool)Get(origins.GetValue(3), "disableFog"), "native update selectively bypasses and restores origin flag");
                Assert((float)Get(sphere, "currentSize") == (float)Get(fog, "currentSize"), "native visual radius matches movement radius");
                Set(fog, "isMoving", false); waits = 0; update.Invoke(fog, null);
                Assert(waits == (volcano ? 1 : 0), "native waiting path remains reachable only for enabled area");
            }
            biome.SetValue(segments.GetValue(3), Enum.Parse(biome.FieldType, "Volcano"));
            setOrigin.Invoke(fog, new object[] { 3 }); Set(fog, "isMoving", false);
            failWait = true; Throws(() => update.Invoke(fog, null)); failWait = false;
            Assert((bool)Get(origins.GetValue(3), "disableFog"), "update exception restores origin flag");
            // These are actual native synchronization setters, not a simulated new protocol.
            sync.Invoke(fog, new object[] { 543f, true }); update.Invoke(fog, null);
            Assert(Math.Abs((float)Get(fog, "currentSize") - (543f - .8f * Time.deltaTime)) < .001f, "incoming host radius is not reset by Update");
            setOrigin.Invoke(fog, new object[] { 3 }); sync.Invoke(fog, new object[] { 432f, false });
            Assert((float)Get(fog, "currentSize") == 432f && !(bool)Get(fog, "isMoving"), "native init-then-sync ordering retains host snapshot");
            Set(map, "currentSegment", 4); Set(fog, "isMoving", true); moves = 0; update.Invoke(fog, null);
            Assert(moves == 0 && (bool)Get(origins.GetValue(3), "disableFog"), "entering final stage leaves previous fog disabled");
            setOrigin.Invoke(fog, new object[] { 4 });
            Assert((bool)Get(fog, "hasArrived") && !sphereRoot.activeSelf, "final checkpoint uses native out-of-range fog shutdown");
            SetRules(0); Set(map, "currentSegment", 3); setOrigin.Invoke(fog, new object[] { 3 });
            Assert((float)Get(fog, "currentSize") == 10000f && (float)Get(fog, "speed") == .4f, "official next run restores native origin and speed");
            Assert(!OwnedPatch(wait), "native rest/host routine remains unpatched");
            var fogPatches = Harmony.GetPatchInfo(sphereType.GetMethod("SetSharderVars", Flags));
            Assert(fogPatches != null && fogPatches.Transpilers.Count == 1 && fogPatches.Prefixes.Count == 1 && fogPatches.Postfixes.Count == 1,
                "fog keeps native body with status-call adaptation and reversible colour hooks");
            return checks;
        }
        finally
        {
            patches.UnpatchSelf(); fogOn = true; failMove = failWait = false;
            for (int i = 0; i < saved.Length; i++) saved[i].SetValue(null, before[i]);
            UnityEngine.Object.DestroyImmediate(root);
        }
        void SetRules(int level) => active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), Math.Max(level, 0), level < 0 ? 1 << (-level - 9) : 0, true, true));
        void Assert(bool result, string reason) { if (!result) throw new Exception(reason); checks++; }
        void Throws(Action action) { try { action(); } catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { checks++; return; } throw new Exception("Expected fog fixture exception"); }
    }
    private static bool OwnedPatch(MethodInfo method) => Harmony.GetPatchInfo(method)?.Owners.Contains("13dastardlyascents") == true;
    private static object Get(object target, string name) => AccessTools.Field(target.GetType(), name).GetValue(target);
    private static void Set(object target, string name, object value) => AccessTools.Field(target.GetType(), name).SetValue(target, value);
    private static bool Ascent(ref int __result) { __result = nativeAscent; return false; }
    private static bool FogEnabled(ref bool __result) { __result = fogOn; return false; }
    private static bool NotMaster(ref bool __result) { __result = false; return false; }
    private static void ObserveMove(object __instance) { moves++; observedSpeed = (float)Get(__instance, "speed"); if (failMove) throw new InvalidOperationException("Expected fog movement fixture exception"); }
    private static bool ObserveWait() { waits++; if (failWait) throw new InvalidOperationException("Expected fog waiting fixture exception"); return false; }
}
