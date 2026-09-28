using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

internal static class CitadelRegression
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    internal static int Run(Assembly mod)
    {
        var coordinator = mod.GetType("dda.RunCoordinator");
        var active = coordinator.GetField("Active", Flags);
        var running = coordinator.GetField("InRun", Flags);
        object oldActive = active.GetValue(null), oldRunning = running.GetValue(null);
        var mapType = AccessTools.TypeByName("MapHandler");
        var fogType = AccessTools.TypeByName("OrbFogHandler");
        var mapInstance = mapType.BaseType.GetField("_instance", Flags);
        var fogInstance = fogType.BaseType.GetField("_instance", Flags);
        object oldMap = mapInstance.GetValue(null), oldFog = fogInstance.GetValue(null);
        var go = new GameObject("Continued native Citadel parameters");
        go.SetActive(false);
        int checks = 0;
        try
        {
            var map = go.AddComponent(mapType); mapInstance.SetValue(null, map);
            var fog = go.AddComponent(fogType); fogInstance.SetValue(null, fog);
            var segmentType = mapType.GetNestedType("MapSegment");
            var segments = Array.CreateInstance(segmentType, 7);
            for (int i = 0; i < 7; i++) segments.SetValue(Activator.CreateInstance(segmentType), i);
            mapType.GetField("segments", Flags).SetValue(map, segments);
            var biomeField = segmentType.GetField("_biome", Flags);
            var segmentField = mapType.GetField("currentSegment", Flags);
            var riseType = AccessTools.TypeByName("LavaRising");
            var rise = go.AddComponent(riseType);
            var fieldType = riseType.GetField("risingFieldType", Flags);
            var required = riseType.GetField("requiredSegment", Flags);
            var prefix = mod.GetType("dda.activeKiln").GetMethod("Prefix", Flags);
            var stateReset = mod.GetType("dda.EffectState").GetMethod("Reset", Flags);
            var wait = riseType.GetField("initialWaitTime", Flags);
            var travel = riseType.GetField("travelTime", Flags);
            running.SetValue(null, true);
            foreach (int level in new[] { 0, 14, 15, 20 })
            foreach (int biome in new[] { 8, 9 }) // Actual scene labels Citadel as Swamp, enum Temple exists but is unused there.
            {
                active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), level, 0, true, true));
                biomeField.SetValue(segments.GetValue(4), Enum.ToObject(biomeField.FieldType, biome));
                segmentField.SetValue(map, 4); required.SetValue(rise, Enum.ToObject(required.FieldType, 4));
                fieldType.SetValue(rise, Enum.Parse(fieldType.FieldType, "Gloom"));
                wait.SetValue(rise, 150f); travel.SetValue(rise, 1200f);
                for (int repeat = 0; repeat < 3; repeat++) prefix.Invoke(null, new[] { rise });
                Assert((float)wait.GetValue(rise) == (level >= 15 ? 30f : 150f), "actual Citadel wait parameter");
                Assert(Math.Abs((float)travel.GetValue(rise) - (level >= 15 ? 480f : 1200f)) < .001f, "Citadel speed is 2.5x of its native 1200-second journey");
                stateReset.Invoke(null, null);
                Assert((float)wait.GetValue(rise) == 150f && (float)travel.GetValue(rise) == 1200f, "native parameters restored on reset");
            }
            active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), 0, 1 << 6, true, true));
            prefix.Invoke(null, new[] { rise });
            Assert(Math.Abs((float)travel.GetValue(rise) - 480f) < .001f, "forced level 15 also changes Citadel");
            stateReset.Invoke(null, null);

            foreach (int level in new[] { 0, 14, 15, 20, -15 })
            {
                active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), Math.Max(0, level), level == -15 ? 1 << 6 : 0, true, true));
                segmentField.SetValue(map, 6); required.SetValue(rise, Enum.ToObject(required.FieldType, 6));
                fieldType.SetValue(rise, Enum.Parse(fieldType.FieldType, "VoidGhosts"));
                wait.SetValue(rise, 0f); travel.SetValue(rise, 620f);
                riseType.GetField("waitForEvent", Flags).SetValue(rise, true);
                riseType.GetProperty("timeTraveled", Flags).SetValue(rise, 155f);
                for (int repeat = 0; repeat < 100; repeat++) prefix.Invoke(null, new[] { rise });
                bool enabled = level >= 15 || level == -15;
                Assert(Math.Abs((float)travel.GetValue(rise) - (enabled ? 248f : 620f)) < .001f, "Nadir speed stays at exactly 2.5x over repeated frames");
                Assert((float)wait.GetValue(rise) == 0f && (bool)riseType.GetField("waitForEvent", Flags).GetValue(rise), "Nadir keeps its native soul event and zero additional wait");
                Assert((float)riseType.GetProperty("timeTraveled", Flags).GetValue(rise) == 155f, "timing override never resets saved/replicated progress");
                stateReset.Invoke(null, null);
                Assert((float)travel.GetValue(rise) == 620f, "Nadir native duration restored at airport");
            }
            segmentField.SetValue(map, 4); required.SetValue(rise, Enum.ToObject(required.FieldType, 4));
            wait.SetValue(rise, 150f); travel.SetValue(rise, 1200f);
            fieldType.SetValue(rise, Enum.Parse(fieldType.FieldType, "VoidGhosts"));
            prefix.Invoke(null, new[] { rise });
            Assert((float)travel.GetValue(rise) == 1200f, "only VoidGhosts in the native Nadir segment gets the new multiplier");
            fieldType.SetValue(rise, Enum.Parse(fieldType.FieldType, "Lava"));
            biomeField.SetValue(segments.GetValue(4), Enum.ToObject(biomeField.FieldType, 3));
            prefix.Invoke(null, new[] { rise });
            Assert((float)travel.GetValue(rise) == 700f, "Kiln retains 700 seconds");
            stateReset.Invoke(null, null);

            // Run the native readiness function. Waiting still pauses during resting.
            var resting = fogType.GetProperty("PlayersAreResting", Flags);
            var ready = riseType.GetMethod("ReadyToStart", Flags);
            resting.SetValue(fog, true);
            Assert(!(bool)ready.Invoke(rise, null), "native resting protection remains");
            resting.SetValue(fog, false);
            riseType.GetField("waitForEvent", Flags).SetValue(rise, true);
            Assert(!(bool)ready.Invoke(rise, null), "native event gate remains");
            riseType.GetField("waitForEvent", Flags).SetValue(rise, false);
            Assert((bool)ready.Invoke(rise, null), "native readiness opens when no resting/event/height gate");

            return checks;
        }
        finally
        {
            mod.GetType("dda.EffectState").GetMethod("Reset", Flags).Invoke(null, null);
            active.SetValue(null, oldActive); running.SetValue(null, oldRunning);
            mapInstance.SetValue(null, oldMap); fogInstance.SetValue(null, oldFog);
            UnityEngine.Object.DestroyImmediate(go);
        }
        void Assert(bool pass, string reason) { if (!pass) throw new Exception(reason); checks++; }
    }
}
