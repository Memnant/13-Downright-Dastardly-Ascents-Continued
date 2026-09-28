using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

internal static class SwampSnowRegression
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static double now;

    internal static int Run(Assembly mod)
    {
        var coordinator = mod.GetType("dda.RunCoordinator");
        var active = coordinator.GetField("Active", Flags); var running = coordinator.GetField("InRun", Flags);
        var resume = coordinator.GetField("ResumePrepared", Flags);
        var mapType = AccessTools.TypeByName("MapHandler"); var mapInstance = mapType.BaseType.GetField("_instance", Flags);
        var runType = AccessTools.TypeByName("RunManager"); var runInstance = runType.GetField("Instance", Flags);
        var zoneType = AccessTools.TypeByName("WindChillZone"); var zoneInstance = zoneType.GetField("instance", Flags);
        var snow = mod.GetType("dda.SnowWeatherSync"); var stateField = snow.GetField("state", Flags);
        var received = snow.GetField("received", Flags);
        var grace = mod.GetType("dda.WeatherGrace"); var graceState = grace.GetField("state", Flags);
        var fields = new[] { active, running, resume, mapInstance, runInstance, zoneInstance, stateField, received, graceState };
        var saved = new object[fields.Length];
        for (int i = 0; i < fields.Length; i++) saved[i] = fields[i].GetValue(null);
        var root = new GameObject("Continued swamp snow fixture"); root.SetActive(false);
        var storm = new GameObject("SnowStorm"); storm.SetActive(false); storm.transform.SetParent(root.transform, false);
        var patches = new Harmony("dda.continued.swamp-snow-fixture");
        int checks = 0;
        try
        {
            patches.Patch(AccessTools.PropertyGetter(coordinator, "IsHost"), prefix: new HarmonyMethod(typeof(SwampSnowRegression), "Host"));
            patches.Patch(AccessTools.PropertyGetter(mod.GetType("dda.TideSync"), "Clock"), prefix: new HarmonyMethod(typeof(SwampSnowRegression), "Clock"));
            var map = root.AddComponent(mapType); mapInstance.SetValue(null, map);
            var segmentType = mapType.GetNestedType("MapSegment"); var segments = Array.CreateInstance(segmentType, 7);
            for (int i = 0; i < segments.Length; i++) segments.SetValue(Activator.CreateInstance(segmentType), i);
            mapType.GetField("segments", Flags).SetValue(map, segments);
            var biome = segmentType.GetField("_biome", Flags);
            var run = root.AddComponent(runType); runInstance.SetValue(null, run);
            var id = Guid.NewGuid(); runType.GetProperty("RunId", Flags).SetValue(run, id);
            running.SetValue(null, true); resume.SetValue(null, false); graceState.SetValue(null, null);
            var zone = storm.AddComponent(zoneType);
            var on = zoneType.GetField("windTimeRangeOn", Flags); var off = zoneType.GetField("windTimeRangeOff", Flags);
            var next = zoneType.GetMethod("GetNextWindTime", Flags);
            foreach (var selection in new[] { (0, 0), (19, 0), (20, 0), (0, 1 << 11) })
            foreach (var region in new[] { (8, 3), (8, 4), (9, 4), (3, 3), (2, 2), (8, 2) })
            foreach (string name in new[] { "SnowStorm", "WindStorm", "RainStorm" })
            {
                active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), selection.Item1, selection.Item2, true, true));
                SetRegion(region.Item1, region.Item2); storm.name = name;
                var activeRange = name == "SnowStorm" ? new Vector2(15, 25) : name == "WindStorm" ? new Vector2(7.5f, 15) : new Vector2(20, 40);
                var restRange = name == "RainStorm" ? new Vector2(40, 80) : new Vector2(30, 90);
                on.SetValue(zone, activeRange); off.SetValue(zone, restRange);
                bool swamp = (selection.Item1 == 20 || selection.Item2 != 0) && region.Item1 == 8 && region.Item2 == 3 && name == "SnowStorm";
                var expectedRest = swamp ? new Vector2(60, 120) : restRange;
                for (int i = 0; i < 16; i++)
                {
                    float rest = (float)next.Invoke(zone, new object[] { false });
                    float duration = (float)next.Invoke(zone, new object[] { true });
                    Assert(rest >= expectedRest.x && rest <= expectedRest.y && duration >= activeRange.x && duration <= activeRange.y,
                        "native snow rest selects current region; wind/rain and active duration unchanged");
                    Assert((Vector2)off.GetValue(zone) == restRange, "rest override does not persist or accumulate");
                }
            }
            active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), 20, 0, true, true));
            SetRegion(8, 3); storm.name = "SnowStorm";
            var stateType = mod.GetType("dda.SnowWeatherState"); var apply = snow.GetMethod("Apply", Flags);
            for (int i = 0; i < 32; i++)
            {
                now = 100;
                var expired = Activator.CreateInstance(stateType);
                stateType.GetField("runId").SetValue(expired, id.ToString("N"));
                stateType.GetField("clock").SetValue(expired, 0d);
                stateType.GetField("duration").SetValue(expired, 20f);
                stateType.GetField("active").SetValue(expired, true);
                stateType.GetField("direction").SetValue(expired, 1);
                stateField.SetValue(null, expired);
                apply.Invoke(null, new[] { zone });
                var rest = stateField.GetValue(null);
                float duration = (float)stateType.GetField("duration").GetValue(rest);
                Assert(!(bool)stateType.GetField("active").GetValue(rest) && duration >= 60 && duration <= 120 &&
                    (bool)stateType.GetProperty("Valid").GetValue(rest), "generated snow publishes a valid 60-120 rest");
                now += 10; apply.Invoke(null, new[] { zone });
                Assert(ReferenceEquals(stateField.GetValue(null), rest) && Math.Abs((float)zoneType.GetField("untilSwitch", Flags).GetValue(zone) - (duration - 10)) < .00001f,
                    "repeated update uses the same host phase and deadline");
                SetRegion(2, 2); apply.Invoke(null, new[] { zone });
                Assert(ReferenceEquals(stateField.GetValue(null), rest), "leaving swamp does not restart an in-progress rest");
                now = 101 + duration; apply.Invoke(null, new[] { zone });
                var playing = stateField.GetValue(null);
                float playingDuration = (float)stateType.GetField("duration").GetValue(playing);
                Assert((bool)stateType.GetField("active").GetValue(playing) && playingDuration >= 15 && playingDuration <= 25,
                    "next snow retains 15-25 second active duration");
                now += playingDuration + 1; apply.Invoke(null, new[] { zone });
                float outsideRest = (float)stateType.GetField("duration").GetValue(stateField.GetValue(null));
                Assert(outsideRest >= 30 && outsideRest <= 90, "next rest outside swamp returns to 30-90");
                SetRegion(8, 3);
            }
            return checks;
            void SetRegion(int kind, int segment)
            {
                mapType.GetField("currentSegment", Flags).SetValue(map, segment);
                biome.SetValue(segments.GetValue(segment), Enum.ToObject(biome.FieldType, kind));
            }
        }
        finally
        {
            patches.UnpatchSelf();
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, saved[i]);
            UnityEngine.Object.DestroyImmediate(root);
        }
        void Assert(bool pass, string reason) { if (!pass) throw new Exception(reason); checks++; }
    }
    private static bool Host(ref bool __result) { __result = true; return false; }
    private static bool Clock(ref double __result) { __result = now; return false; }
}
