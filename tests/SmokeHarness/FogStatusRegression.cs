using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

internal static class FogStatusRegression
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static object target;
    private static Vector3 position;
    private static int calls;
    private static string deliveredType;
    private static float deliveredAmount;
    private static bool deliveredFlags, deliveredIgnoreInvincibility, deliveredIgnoreSkeleton, allowNative, returned, throwOnStatus;

    internal static int Run(Assembly mod)
    {
        var coordinator = mod.GetType("dda.RunCoordinator");
        var active = coordinator.GetField("Active", Flags); var running = coordinator.GetField("InRun", Flags);
        var characterType = AccessTools.TypeByName("Character"); var local = characterType.GetField("localCharacter", Flags);
        var mapType = AccessTools.TypeByName("MapHandler"); var mapInstance = mapType.BaseType.GetField("_instance", Flags);
        var replicationDepth = mod.GetType("dda.StatusReplication").GetField("depth", Flags);
        var fields = new[] { active, running, local, mapInstance, replicationDepth };
        var saved = new object[fields.Length];
        for (int i = 0; i < fields.Length; i++) saved[i] = fields[i].GetValue(null);
        var root = new GameObject("Continued fog status fixture"); root.SetActive(false);
        var patches = new Harmony("dda.continued.fog-status-fixture");
        int checks = 0;
        float oldEnabled = Shader.GetGlobalFloat("FogEnabled"), oldSize = Shader.GetGlobalFloat("_FogSphereSize");
        Vector4 oldCenter = Shader.GetGlobalVector("FogCenter");
        try
        {
            var viewType = AccessTools.TypeByName("Photon.Pun.PhotonView"); var view = root.AddComponent(viewType);
            viewType.GetField("<IsMine>k__BackingField", Flags).SetValue(view, true);
            var character = root.AddComponent(characterType); local.SetValue(null, character);
            characterType.GetField("view", Flags).SetValue(character, view);
            var dataField = characterType.GetField("data", Flags); var data = root.AddComponent(dataField.FieldType);
            dataField.SetValue(character, data); Set(data, "character", character);
            var afflictionType = AccessTools.TypeByName("CharacterAfflictions"); target = root.AddComponent(afflictionType);
            Set(target, "character", character);
            Set(target, "currentStatuses", new float[20]); Set(target, "currentIncrementalStatuses", new float[20]);
            Set(target, "currentDecrementalStatuses", new float[20]);
            var refsField = characterType.GetField("refs", Flags); var refs = Activator.CreateInstance(refsField.FieldType);
            refsField.FieldType.GetField("afflictions", Flags).SetValue(refs, target); refsField.SetValue(character, refs);
            var map = root.AddComponent(mapType); mapInstance.SetValue(null, map);
            var segmentType = mapType.GetNestedType("MapSegment"); var segments = Array.CreateInstance(segmentType, 7);
            for (int i = 0; i < segments.Length; i++) segments.SetValue(Activator.CreateInstance(segmentType), i);
            Set(map, "segments", segments); var biome = segmentType.GetField("_biome", Flags);
            var renderer = root.AddComponent<MeshRenderer>();
            var fogType = AccessTools.TypeByName("FogSphere"); var fog = root.AddComponent(fogType);
            Set(fog, "rend", renderer); Set(fog, "currentSize", 50f); Set(fog, "fogPoint", Vector3.zero); Set(fog, "ENABLE", 1f);
            var props = new MaterialPropertyBlock(); var tint = new Color(.11f, .22f, .33f, .44f);
            props.SetColor("_FogtintColor", tint); renderer.SetPropertyBlock(props);
            var update = fogType.GetMethod("SetSharderVars", Flags); var add = afflictionType.GetMethod("AddStatus", Flags);
            patches.Patch(AccessTools.PropertyGetter(characterType, "Center"), prefix: new HarmonyMethod(typeof(FogStatusRegression), "Center"));
            patches.Patch(add, prefix: new HarmonyMethod(typeof(FogStatusRegression), "Capture") { priority = Priority.Last },
                postfix: new HarmonyMethod(typeof(FogStatusRegression), "Result"));
            running.SetValue(null, true); replicationDepth.SetValue(null, 0);

            foreach (var selection in new[] { (0, 0), (9, 0), (13, 0), (14, 0), (16, 0), (17, 0), (18, 0), (19, 0), (20, 0),
                (0, 1 << 5), (0, 1 << 8), (0, (1 << 5) | (1 << 8)), (0, 1 << 9), (0, 1 << 11) })
            foreach (var region in new[] { (0, 0), (2, 2), (3, 3), (8, 3), (8, 4), (9, 4), (10, 6) })
            foreach (bool skeleton in new[] { false, true })
            foreach (int location in new[] { 0, 1, 2, 3 })
            {
                Select(selection.Item1, selection.Item2); SetRegion(region.Item1, region.Item2); Set(data, "_isSkeleton", skeleton);
                // Locations: inside, exactly on native boundary, outside, disabled fog.
                position = Vector3.right * (location == 0 ? 10 : location == 1 ? 50 : 100);
                Set(fog, "ENABLE", location == 3 ? 0f : 1f);
                bool emits = location == 2;
                bool tier14 = selection.Item1 >= 14 || (selection.Item2 & (1 << 5)) != 0;
                bool tier17 = selection.Item1 >= 17 || (selection.Item2 & (1 << 8)) != 0;
                bool swamp = region.Item1 == 8 && region.Item2 == 3 && (selection.Item1 >= 18 || (selection.Item2 & (1 << 9)) != 0);
                // Full Swamp field/geometry/protection coverage lives in SwampFogRegression.
                // This fixture intentionally has no ambient field, so its extension fails closed.
                if (swamp) emits = false;
                string kind = skeleton ? "Injury" : !tier14 ? "Cold" : region.Item1 == 3 ? "Hot" : "Poison";
                float rate = .0105f / (skeleton ? 8 : 1);
                if (tier14) rate *= skeleton || region.Item1 != 3 ? 3 : 6;
                if (tier17) rate *= kind == "Cold" ? 1.3f : kind == "Hot" ? 1.5f : kind == "Poison" ? 1.75f : 1f;
                calls = 0; update.Invoke(fog, null);
                Assert(calls == (emits ? 1 : 0), "native fog boundary and enabled checks submit at most one status");
                if (emits)
                {
                    Assert(deliveredType == kind && Math.Abs(deliveredAmount - rate * Time.deltaTime) < 1e-7f,
                        "old fog status/rate with tier-17 scaling once: tier=" + selection + "/region=" + region + "/skeleton=" + skeleton +
                        "/type=" + deliveredType + "/amount=" + deliveredAmount + "/expected=" + rate * Time.deltaTime);
                    Assert(deliveredFlags, "native RPC/effects/notify/invincibility flags reach AddStatus unchanged");
                }
                Assert((bool)Get(data, "isInFog") == (location == 2), "native isInFog result retained");
                renderer.GetPropertyBlock(props);
                var expectedTint = tier14 && region.Item1 == 3 ? new Color(.3f, 0, 0, 0) : swamp ? new Color(.45f, .35f, .55f, 0) : tint;
                Assert(props.GetFloat("_FogDepth") == 50f && props.GetColor("_FogtintColor") == expectedTint,
                    "native rendering parameters and scoped tint selected");
            }
            Select(20); SetRegion(0, 0); Set(data, "_isSkeleton", false); Set(fog, "ENABLE", 1f); position = Vector3.right * 100;
            for (int i = 0; i < 10; i++)
            {
                calls = 0; update.Invoke(fog, null);
                Assert(calls == 1 && deliveredType == "Poison" && Math.Abs(deliveredAmount - .055125f * Time.deltaTime) < 1e-7f,
                    "repeated fog ticks neither duplicate delivery nor compound the multiplier");
            }
            var statusType = afflictionType.GetNestedType("STATUSTYPE");
            Select(0);
            var bridge = mod.GetType("dda.Tier14FogStatus").GetMethod("AddFogStatus", Flags);
            foreach (bool ignoreSkeleton in new[] { false, true })
            {
                bridge.Invoke(null, new object[] { target, Enum.Parse(statusType, "Cold"), .001f, false, true, true, false, ignoreSkeleton });
                Assert(deliveredIgnoreSkeleton == ignoreSkeleton, "2.5 fog bridge forwards the skeleton flag unchanged");
            }
            Select(20);
            Direct("Cold", .1f); Assert(deliveredType == "Cold" && Math.Abs(deliveredAmount - .13f) < 1e-7f, "cold outside FogSphere is not converted");
            Direct("Poison", .1f); Assert(deliveredType == "Poison" && Math.Abs(deliveredAmount - .175f) < 1e-7f, "other poison does not get fog triple multiplier");
            Set(data, "_isSkeleton", true); Set(data, "isInFog", false); Direct("Injury", .001f);
            Assert(deliveredType == "Injury" && Math.Abs(deliveredAmount - .099f) < 1e-7f,
                "tier-12 skeleton ordinary injury still receives its existing multiplier outside fog");
            throwOnStatus = true; bool skeletonThrew = false;
            try { update.Invoke(fog, null); } catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { skeletonThrew = true; }
            throwOnStatus = false;
            Assert(skeletonThrew && !(bool)Get(data, "isInFog"), "skeleton fog marker is restored even when AddStatus throws");
            Set(data, "_isSkeleton", false);
            mapInstance.SetValue(null, null); update.Invoke(fog, null);
            Assert(deliveredType == "Cold" && Math.Abs(deliveredAmount - .01365f * Time.deltaTime) < 1e-7f, "loading with no map retains native type and normal tier-17 behavior");
            mapInstance.SetValue(null, map); Set(map, "segments", null); update.Invoke(fog, null);
            Assert(deliveredType == "Cold", "partially initialized map safely skips biome conversion"); Set(map, "segments", segments);
            local.SetValue(null, null); calls = 0; update.Invoke(fog, null);
            Assert(calls == 0, "no local character means no fog status"); local.SetValue(null, character);
            running.SetValue(null, false); update.Invoke(fog, null);
            Assert(deliveredType == "Cold" && Math.Abs(deliveredAmount - .0105f * Time.deltaTime) < 1e-7f, "airport or next official run returns to native cold without residual state");
            running.SetValue(null, true); replicationDepth.SetValue(null, 1); update.Invoke(fog, null);
            Assert(deliveredType == "Cold" && Math.Abs(deliveredAmount - .0105f * Time.deltaTime) < 1e-7f, "replicated state cannot be amplified by the adapter"); replicationDepth.SetValue(null, 0);
            throwOnStatus = true; bool threw = false;
            try { update.Invoke(fog, null); } catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { threw = true; }
            throwOnStatus = false; Direct("Cold", .1f);
            Assert(threw && deliveredType == "Cold" && Math.Abs(deliveredAmount - .13f) < 1e-7f, "exception leaves no global fog conversion scope");
            // Exercise the real AddStatus early immunity gates (no HUD/network needed).
            allowNative = true; characterType.GetProperty("statusesLocked", Flags).SetValue(character, true);
            update.Invoke(fog, null); Assert(!returned, "native status lock can reject restored fog damage");
            characterType.GetProperty("statusesLocked", Flags).SetValue(character, false); Set(data, "isInvincible", true);
            // Tier 12 intentionally weakens ordinary invincibility; preserve that
            // rule and test native rejection with only tier 14 forced on.
            allowNative = false; update.Invoke(fog, null);
            Assert(deliveredIgnoreInvincibility && Math.Abs(deliveredAmount - .055125f * .25f * Time.deltaTime) < 1e-7f,
                "tier-12 ordinary invincibility still permits quarter fog status");
            Select(0, 1 << 5); allowNative = true;
            update.Invoke(fog, null); Assert(!returned, "native invincibility still rejects restored poison fog");
            Set(data, "isInvincible", false); allowNative = false;
            return checks;

            void Select(int level, int mask = 0) => active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), level, mask, true, true));
            void SetRegion(int kind, int segment)
            {
                Set(map, "currentSegment", segment); biome.SetValue(segments.GetValue(segment), Enum.ToObject(biome.FieldType, kind));
            }
            void Direct(string kind, float amount) => add.Invoke(target, new object[] { Enum.Parse(statusType, kind), amount, false, true, true, false, false });
        }
        finally
        {
            patches.UnpatchSelf(); target = null; allowNative = throwOnStatus = false;
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, saved[i]);
            UnityEngine.Object.DestroyImmediate(root);
            Shader.SetGlobalFloat("FogEnabled", oldEnabled); Shader.SetGlobalFloat("_FogSphereSize", oldSize); Shader.SetGlobalVector("FogCenter", oldCenter);
        }
        void Assert(bool pass, string reason) { if (!pass) throw new Exception(reason); checks++; }
    }
    private static bool Center(ref Vector3 __result) { __result = position; return false; }
    private static bool Capture(object __instance, object statusType, float amount, bool fromRPC, bool playEffects, bool notify, bool ignoreInvincibility, bool ignoreSkeleton, ref bool __result)
    {
        if (!ReferenceEquals(__instance, target)) return true;
        if (throwOnStatus) throw new InvalidOperationException("Expected fog fixture failure");
        calls++; deliveredType = statusType.ToString(); deliveredAmount = amount;
        deliveredIgnoreInvincibility = ignoreInvincibility;
        deliveredIgnoreSkeleton = ignoreSkeleton;
        deliveredFlags = !fromRPC && playEffects && notify && !ignoreInvincibility && !ignoreSkeleton;
        if (allowNative) return true;
        __result = true; return false;
    }
    private static void Result(object __instance, bool __result) { if (ReferenceEquals(__instance, target)) returned = __result; }
    private static object Get(object instance, string name) => AccessTools.Field(instance.GetType(), name).GetValue(instance);
    private static void Set(object instance, string name, object value) => AccessTools.Field(instance.GetType(), name).SetValue(instance, value);
}
