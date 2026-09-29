using System;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using UnityEngine;

internal static class GloomGrowthRegression
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static object fixtureAfflictions;
    private static int calls, hazard;
    private static float delivered;

    internal static int Run(Assembly mod)
    {
        var coordinator = mod.GetType("dda.RunCoordinator");
        var active = coordinator.GetField("Active", Flags); var running = coordinator.GetField("InRun", Flags);
        var characterType = AccessTools.TypeByName("Character"); var local = characterType.GetField("localCharacter", Flags);
        var mapType = AccessTools.TypeByName("MapHandler"); var mapInstance = mapType.BaseType.GetField("_instance", Flags);
        object oldActive = active.GetValue(null), oldRunning = running.GetValue(null), oldLocal = local.GetValue(null), oldMap = mapInstance.GetValue(null);
        var root = new GameObject("Continued native gloom growth fixture"); root.SetActive(false);
        var harness = new Harmony("dda.continued.gloom-growth-fixture");
        int checks = 0;
        try
        {
            var character = root.AddComponent(characterType); local.SetValue(null, character);
            var data = characterType.GetField("data", Flags); data.SetValue(character, Activator.CreateInstance(data.FieldType));
            var afflictionType = AccessTools.TypeByName("CharacterAfflictions");
            fixtureAfflictions = FormatterServices.GetUninitializedObject(afflictionType);
            afflictionType.GetField("character", Flags).SetValue(fixtureAfflictions, character);
            var refsField = characterType.GetField("refs", Flags); var refs = Activator.CreateInstance(refsField.FieldType);
            refsField.FieldType.GetField("afflictions", Flags).SetValue(refs, fixtureAfflictions); refsField.SetValue(character, refs);
            var map = root.AddComponent(mapType); mapInstance.SetValue(null, map);
            var segmentType = mapType.GetNestedType("MapSegment"); var segments = Array.CreateInstance(segmentType, 5);
            for (int i = 0; i < 5; i++) segments.SetValue(Activator.CreateInstance(segmentType), i);
            mapType.GetField("segments", Flags).SetValue(map, segments); mapType.GetField("currentSegment", Flags).SetValue(map, 3);
            var biome = segmentType.GetField("_biome", Flags); biome.SetValue(segments.GetValue(3), Enum.ToObject(biome.FieldType, 8));
            var gloomType = AccessTools.TypeByName("Peak.StatusFieldGloom"); var gloom = root.AddComponent(gloomType);
            var baseType = AccessTools.TypeByName("Peak.StatusFieldBase");
            var status = baseType.GetField("statusType", Flags); status.SetValue(gloom, Enum.Parse(status.FieldType, "Drowsy"));
            baseType.GetField("statusAmountPerSecond", Flags).SetValue(gloom, .1f);
            var extras = baseType.GetField("additionalStatuses", Flags); extras.SetValue(gloom, Activator.CreateInstance(extras.FieldType));
            var settings = AccessTools.TypeByName("RunSettings");
            harness.Patch(AccessTools.Method(settings, "GetValue", new[] { settings.GetNestedType("SETTINGTYPE"), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(GloomGrowthRegression), "Hazard"));
            harness.Patch(AccessTools.Method(afflictionType, "AddStatus"),
                prefix: new HarmonyMethod(typeof(GloomGrowthRegression), "Capture") { priority = Priority.Last });
            var tick = gloomType.GetMethod("TickStatus", Flags); running.SetValue(null, true);
            foreach (var selection in new[] { (0, 0), (14, 0), (15, 0), (16, 0), (17, 0), (18, 0), (19, 0), (20, 0),
                (0, 1 << 6), (0, 1 << 8), (0, 1 << 11), (0, (1 << 8) | (1 << 11)), (19, 1 << 11) })
            foreach (var region in new[] { (8, 3), (8, 4), (9, 4), (3, 3), (2, 2), (9, 3) })
            foreach (int enabled in new[] { 0, 1 })
            {
                int level = selection.Item1, mask = selection.Item2;
                active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), level, mask, true, true));
                mapType.GetField("currentSegment", Flags).SetValue(map, region.Item2);
                biome.SetValue(segments.GetValue(region.Item2), Enum.ToObject(biome.FieldType, region.Item1));
                bool extra = (level >= 17 || (mask & (1 << 8)) != 0) &&
                    !((level == 20 || (mask & (1 << 11)) != 0) && region.Item1 == 8 && region.Item2 == 3);
                hazard = enabled;
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    calls = 0; delivered = 0; tick.Invoke(gloom, null);
                    Assert(calls == enabled, "native gloom hazard switch keeps control");
                    if (enabled != 0) Assert(Math.Abs(delivered - .1f * Time.deltaTime * (extra ? 1.3f : 1f)) < 1e-7f,
                        "gloom growth respects tier-20 swamp exemption and native hazard switch; region=" + region + "/tier=" + level);
                }
            }
            active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), 20, 0, true, true));
            mapType.GetField("currentSegment", Flags).SetValue(map, 3);
            biome.SetValue(segments.GetValue(3), Enum.ToObject(biome.FieldType, 8));
            var add = afflictionType.GetMethod("AddStatus", Flags);
            foreach (string kind in new[] { "Drowsy", "Cold", "Hot", "Poison" })
            {
                float multiplier = kind == "Drowsy" ? 1f : kind == "Cold" ? 1.3f : kind == "Hot" ? 1.5f : 1.75f;
                foreach (float input in new[] { -.1f, 0f, .1f })
                {
                    add.Invoke(fixtureAfflictions, new object[] { Enum.Parse(status.FieldType, kind), input, false, false, false, false, false });
                    Assert(Math.Abs(delivered - input * (input > 0 ? multiplier : 1f)) < 1e-7f,
                        "only positive sleep loses tier-17 scaling in swamp; direct sources and other statuses retain their rules");
                }
            }
            // No cached exemption can follow the character into Citadel, another run or loading.
            object drowsy = Enum.Parse(status.FieldType, "Drowsy");
            mapType.GetField("currentSegment", Flags).SetValue(map, 4);
            biome.SetValue(segments.GetValue(4), Enum.ToObject(biome.FieldType, 8));
            CheckSleep(.13f, "Citadel with the same Swamp biome label keeps the multiplier");
            mapType.GetField("currentSegment", Flags).SetValue(map, 3);
            CheckSleep(.1f, "entering swamp applies exemption immediately");
            mapInstance.SetValue(null, null); CheckSleep(.13f, "unknown region cannot receive the swamp exemption");
            mapInstance.SetValue(null, map); running.SetValue(null, false); CheckSleep(.1f, "airport has no tier-17 multiplier");
            running.SetValue(null, true);
            void CheckSleep(float expected, string reason)
            {
                add.Invoke(fixtureAfflictions, new object[] { drowsy, .1f, false, false, false, false, false });
                Assert(Math.Abs(delivered - expected) < 1e-7f, reason);
            }
            return checks;
        }
        finally
        {
            harness.UnpatchSelf(); fixtureAfflictions = null;
            active.SetValue(null, oldActive); running.SetValue(null, oldRunning); local.SetValue(null, oldLocal); mapInstance.SetValue(null, oldMap);
            UnityEngine.Object.DestroyImmediate(root);
        }
        void Assert(bool pass, string reason) { if (!pass) throw new Exception(reason); checks++; }
    }
    private static bool Hazard(object setting, ref int __result)
    { if (setting.ToString() != "Hazard_SleepyGloom") return true; __result = hazard; return false; }
    private static bool Capture(object __instance, float amount)
    { if (!ReferenceEquals(__instance, fixtureAfflictions)) return true; delivered = amount; calls++; return false; }
}
