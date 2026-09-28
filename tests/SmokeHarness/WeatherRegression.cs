using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

internal static class WeatherRegression
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static object observer;
    private static Vector3 position;
    private static int applications;

    internal static int Run(Assembly mod)
    {
        var coordinator = mod.GetType("dda.RunCoordinator");
        var active = coordinator.GetField("Active", Flags);
        var running = coordinator.GetField("InRun", Flags);
        var characterType = AccessTools.TypeByName("Character");
        var local = characterType.GetField("localCharacter", Flags);
        var zoneType = AccessTools.TypeByName("WindChillZone");
        var singleton = zoneType.GetField("instance", Flags);
        object oldActive = active.GetValue(null), oldRunning = running.GetValue(null), oldLocal = local.GetValue(null), oldZone = singleton.GetValue(null);
        var reset = mod.GetType("dda.EffectState").GetMethod("Reset", Flags);
        var harness = new Harmony("dda.continued.weather-fixture");
        var fixture = new GameObject("Continued native weather regression");
        fixture.SetActive(false);
        int checks = 0;
        try
        {
            observer = fixture.AddComponent(characterType);
            local.SetValue(null, observer);
            var zone = fixture.AddComponent(zoneType);
            var bounds = zoneType.GetField("windZoneBounds", Flags);
            var awake = zoneType.GetMethod("Awake", Flags);
            var update = zoneType.GetMethod("Update", Flags);
            var wind = zoneType.GetField("windActive", Flags);
            var slippy = zoneType.GetField("setSlippy", Flags);
            var on = zoneType.GetField("windTimeRangeOn", Flags); var off = zoneType.GetField("windTimeRangeOff", Flags);
            var nextTime = zoneType.GetMethod("GetNextWindTime", Flags);
            zoneType.GetField("windIntensityCurve", Flags).SetValue(zone, AnimationCurve.Linear(0, 1, 1, 1));
            // Keep native Update, Contains and both membership flags. Only replace networking,
            // the synthetic character's position and the final status delivery boundary.
            harness.Patch(zoneType.GetMethod("HandleTime", Flags), prefix: new HarmonyMethod(typeof(WeatherRegression), "SkipNetworkClock"));
            harness.Patch(zoneType.GetMethod("ApplyStatus", Flags), prefix: new HarmonyMethod(typeof(WeatherRegression), "CaptureStatus") { priority = Priority.First });
            harness.Patch(AccessTools.PropertyGetter(characterType, "Center"), prefix: new HarmonyMethod(typeof(WeatherRegression), "CharacterPosition"));
            harness.Patch(AccessTools.PropertyGetter(characterType, "observedCharacter"), prefix: new HarmonyMethod(typeof(WeatherRegression), "ObservedCharacter"));

            foreach (string name in new[] { "RainStorm", "WindStorm", "SnowStorm" })
            foreach (int selection in new[] { 0, 19, 20, -20 })
            {
                reset.Invoke(null, null);
                fixture.name = name;
                bool enabled = selection == 20 || selection == -20;
                active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), Math.Max(0, selection), selection == -20 ? 1 << 11 : 0, true, true));
                running.SetValue(null, true);
                // Values from PEAK 2.4.c level6/7/8 serialized components.
                Vector3 size = name == "SnowStorm" ? new Vector3(538.2f, 414.4f, 473.4f) : new Vector3(538.2f, 381.8f, 307.8f);
                fixture.transform.position = new Vector3(0, 580, 841);
                var baseline = new Bounds(fixture.transform.position, size);
                bounds.SetValue(zone, baseline); slippy.SetValue(zone, false); wind.SetValue(zone, false);
                Vector2 originalOn = name == "SnowStorm" ? new Vector2(15, 25) : name == "WindStorm" ? new Vector2(7.5f, 15) : new Vector2(20, 40);
                Vector2 originalOff = name == "SnowStorm" ? new Vector2(20, 90) : name == "WindStorm" ? new Vector2(60, 120) : new Vector2(40, 80);
                on.SetValue(zone, originalOn); off.SetValue(zone, originalOff);
                position = baseline.center + new Vector3(800, 0, 0);
                Assert(!baseline.Contains(position), "fixture player is outside native weather");
                awake.Invoke(zone, null);
                var expanded = new Bounds(baseline.center, enabled ? size + Vector3.one * 3000 : size);
                Assert((Bounds)bounds.GetValue(zone) == expanded, "native Awake writes weather bounds back for " + name + "/" + selection);
                Assert((bool)slippy.GetValue(zone) == enabled, "wet/slippery effect is scoped to level 20");
                Assert(!(bool)wind.GetValue(zone), "native storm clock is not forced into an uninitialized active phase");
                Vector2 expectedOff = enabled && name != "RainStorm" ? new Vector2(30, 90) : originalOff;
                Assert((Vector2)off.GetValue(zone) == expectedOff && (Vector2)on.GetValue(zone) == originalOn, "only level-20 wind/snow rest interval changes; duration/rain unchanged");
                for (int sample = 0; sample < 32; sample++)
                {
                    float interval = (float)nextTime.Invoke(zone, new object[] { false });
                    float duration = (float)nextTime.Invoke(zone, new object[] { true });
                    Assert(interval >= expectedOff.x && interval <= expectedOff.y && duration >= originalOn.x && duration <= originalOn.y,
                        "native clock selects requested independent interval and existing active duration");
                }
                wind.SetValue(zone, true);
                zoneType.GetField("untilSwitch", Flags).SetValue(zone, 10f);
                zoneType.GetField("timeUntilNextWind", Flags).SetValue(zone, 20f);
                applications = 0;
                update.Invoke(zone, null);
                Assert(applications == (enabled ? 1 : 0), "native Update reaches status delivery outside the original biome");
                Assert((bool)zoneType.GetField("localCharacterInsideBounds", Flags).GetValue(zone) == enabled, "local physical weather membership");
                Assert((bool)zoneType.GetField("observedCharacterInsideBounds", Flags).GetValue(zone) == enabled, "visual weather membership uses the same bounds");
                for (int i = 0; i < 10; i++) { awake.Invoke(zone, null); update.Invoke(zone, null); }
                Assert((Bounds)bounds.GetValue(zone) == expanded, "repeated Awake/Update does not compound range");
                zoneType.GetMethod("RPCA_ToggleWind", Flags).Invoke(zone, new object[] { false, Vector3.forward, 60f });
                int before = applications; update.Invoke(zone, null);
                Assert(applications == before && !(bool)wind.GetValue(zone), "native RPC can stop a storm normally");
                reset.Invoke(null, null);
                Assert((Bounds)bounds.GetValue(zone) == baseline && !(bool)slippy.GetValue(zone), "airport reset restores bounds and slipperiness");
                Assert((Vector2)off.GetValue(zone) == originalOff, "airport reset restores native rest interval");

                if (!enabled) continue;
                running.SetValue(null, false); awake.Invoke(zone, null);
                Assert((Bounds)bounds.GetValue(zone) == baseline, "Awake before room rules leaves official bounds");
                running.SetValue(null, true); wind.SetValue(zone, true); applications = 0;
                update.Invoke(zone, null);
                Assert((Bounds)bounds.GetValue(zone) == expanded && applications == 1, "first Update after late rules applies expanded weather");
                var external = new Bounds(baseline.center, size * 2);
                bounds.SetValue(zone, external); reset.Invoke(null, null);
                Assert((Bounds)bounds.GetValue(zone) == external, "reset preserves a later change made by another mod");
            }
            return checks;
        }
        finally
        {
            reset.Invoke(null, null);
            harness.UnpatchSelf(); observer = null;
            active.SetValue(null, oldActive); running.SetValue(null, oldRunning); local.SetValue(null, oldLocal); singleton.SetValue(null, oldZone);
            UnityEngine.Object.DestroyImmediate(fixture);
        }
        void Assert(bool pass, string reason) { if (!pass) throw new Exception(reason); checks++; }
    }
    private static bool SkipNetworkClock() => false;
    private static bool CaptureStatus() { applications++; return false; }
    private static bool CharacterPosition(ref Vector3 __result) { __result = position; return false; }
    private static bool ObservedCharacter(ref object __result) { __result = observer; return false; }
}
