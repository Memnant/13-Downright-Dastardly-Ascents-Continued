using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

internal static class WeatherGraceRegression
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static double now;
    private static bool master;
    private static int publications, statuses, forces;
    private static Component observer;

    internal static int Run(Assembly mod)
    {
        var coordinator = mod.GetType("dda.RunCoordinator");
        var active = coordinator.GetField("Active", Flags); var running = coordinator.GetField("InRun", Flags);
        var resume = coordinator.GetField("ResumePrepared", Flags);
        var grace = mod.GetType("dda.WeatherGrace"); var stateField = grace.GetField("state", Flags);
        var received = grace.GetField("received", Flags);
        var snow = mod.GetType("dda.SnowWeatherSync"); var snowState = snow.GetField("state", Flags);
        var snowReceived = snow.GetField("received", Flags);
        var characterType = AccessTools.TypeByName("Character"); var local = characterType.GetField("localCharacter", Flags);
        var runType = AccessTools.TypeByName("RunManager"); var runInstance = runType.GetField("Instance", Flags);
        var mapType = AccessTools.TypeByName("MapHandler"); var mapInstance = mapType.BaseType.GetField("_instance", Flags);
        var zoneType = AccessTools.TypeByName("WindChillZone"); var zoneInstance = zoneType.GetField("instance", Flags);
        var dayType = AccessTools.TypeByName("DayNightManager"); var dayInstance = dayType.GetField("instance", Flags);
        var loading = AccessTools.TypeByName("LoadingScreenHandler").GetField("_loading", Flags);
        var tornadoTimer = mod.GetType("dda.allSun").GetField("timeTillNext", Flags);
        FieldInfo[] savedFields = { active, running, resume, stateField, received, local, runInstance, mapInstance,
            zoneInstance, dayInstance, loading, tornadoTimer, snowState, snowReceived };
        object[] savedValues = new object[savedFields.Length];
        for (int i = 0; i < savedFields.Length; i++) savedValues[i] = savedFields[i].GetValue(null);
        var root = new GameObject("Continued weather grace fixture"); root.SetActive(false);
        var stormRoot = new GameObject("SnowStorm"); stormRoot.SetActive(false);
        var particleRoot = new GameObject("Grace particle fixture"); particleRoot.SetActive(false);
        var patches = new Harmony("dda.continued.weather-grace-fixture");
        int checks = 0;
        try
        {
            patches.Patch(AccessTools.PropertyGetter(coordinator, "IsHost"), prefix: new HarmonyMethod(typeof(WeatherGraceRegression), "Host"));
            patches.Patch(AccessTools.PropertyGetter(mod.GetType("dda.TideSync"), "Clock"), prefix: new HarmonyMethod(typeof(WeatherGraceRegression), "Clock"));
            patches.Patch(grace.GetMethod("Publish", Flags), prefix: new HarmonyMethod(typeof(WeatherGraceRegression), "Publish"));
            patches.Patch(AccessTools.PropertyGetter(characterType, "Center"), prefix: new HarmonyMethod(typeof(WeatherGraceRegression), "Center"));
            patches.Patch(AccessTools.PropertyGetter(characterType, "observedCharacter"), prefix: new HarmonyMethod(typeof(WeatherGraceRegression), "Observed"));
            patches.Patch(zoneType.GetMethod("HandleTime", Flags), prefix: new HarmonyMethod(typeof(WeatherGraceRegression), "SkipNetwork") { priority = Priority.Last });
            patches.Patch(zoneType.GetMethod("ApplyStatus", Flags), prefix: new HarmonyMethod(typeof(WeatherGraceRegression), "Status") { priority = Priority.Last });
            patches.Patch(zoneType.GetMethod("AddWindForceToCharacter", Flags), prefix: new HarmonyMethod(typeof(WeatherGraceRegression), "Force"));
            var character = root.AddComponent(characterType); observer = character; local.SetValue(null, character);
            var data = root.AddComponent(AccessTools.TypeByName("CharacterData")); Set(character, "data", data); Set(data, "character", character);
            var afflictions = root.AddComponent(AccessTools.TypeByName("CharacterAfflictions"));
            var refsField = characterType.GetField("refs", Flags); var references = Activator.CreateInstance(refsField.FieldType);
            refsField.FieldType.GetField("afflictions", Flags).SetValue(references, afflictions); refsField.SetValue(character, references);
            var run = root.AddComponent(runType); runInstance.SetValue(null, run);
            var map = root.AddComponent(mapType); mapInstance.SetValue(null, map); Set(map, "currentSegment", 0);
            var day = root.AddComponent(dayType); dayInstance.SetValue(null, day);
            var zone = stormRoot.AddComponent(zoneType);
            Set(zone, "windZoneBounds", new Bounds(Vector3.zero, Vector3.one * 100));
            Set(zone, "windIntensityCurve", AnimationCurve.Linear(0, 1, 1, 1));
            var visualType = AccessTools.TypeByName("StormVisual"); var visual = stormRoot.AddComponent(visualType);
            var particles = particleRoot.AddComponent<ParticleSystem>();
            Set(visual, "zone", zone); Set(visual, "part", new[] { particles });
            var arm = grace.GetMethod("Arm", Flags); var tick = grace.GetMethod("Tick", Flags);
            var reset = grace.GetMethod("Reset", Flags); var blocking = grace.GetProperty("Blocking", Flags);
            var update = zoneType.GetMethod("Update", Flags); var physics = zoneType.GetMethod("FixedUpdate", Flags);
            var rpc = zoneType.GetMethod("RPCA_ToggleWind", Flags);
            var stateType = mod.GetType("dda.WeatherGraceState");
            var runId = runType.GetProperty("RunId", Flags);
            var id = Guid.NewGuid(); runId.SetValue(run, id);
            running.SetValue(null, true); resume.SetValue(null, false); master = true; now = 100;
            SetRules(20); Set(run, "runStarted", true); Set(map, "hasFinishedStartRoutine", true);
            tornadoTimer.SetValue(null, 42f); publications = 0;
            loading.SetValue(null, true); Set(data, "passedOutOnTheBeach", 3f);
            arm.Invoke(null, new object[] { Guid.Empty }); tick.Invoke(null, null);
            Assert(!Started() && IsBlocked(), "loading holds weather without consuming grace");
            loading.SetValue(null, false); Set(map, "hasFinishedStartRoutine", false); tick.Invoke(null, null);
            Assert(!Started(), "map initialization must finish");
            Set(map, "hasFinishedStartRoutine", true);
            refsField.SetValue(character, null); tick.Invoke(null, null);
            Assert(!Started(), "partially initialized character cannot start grace or throw");
            refsField.SetValue(character, references);
            characterType.GetProperty("warping", Flags).SetValue(character, true); tick.Invoke(null, null);
            Assert(!Started(), "spawn warp does not consume grace");
            characterType.GetProperty("warping", Flags).SetValue(character, false); tick.Invoke(null, null);
            Assert(!Started(), "beach wake-up timer must finish");
            Set(data, "passedOutOnTheBeach", 0f); Set(run, "runStarted", false); tick.Invoke(null, null);
            Assert(!Started(), "native run must start first");
            Set(run, "runStarted", true); runId.SetValue(run, Guid.Empty); tick.Invoke(null, null);
            Assert(!Started(), "missing RunId cannot start window");
            runId.SetValue(run, id); now = 300; tick.Invoke(null, null);
            Assert(Started() && (double)Get(stateField.GetValue(null), "clock") == 300d && IsBlocked(), "ready host starts exactly 30 seconds");
            now = 310; tick.Invoke(null, null); master = false; tick.Invoke(null, null); master = true; tick.Invoke(null, null);
            Assert(publications == 2 && (double)Get(stateField.GetValue(null), "clock") == 300d,
                "arm/start publish once; simulated host changes do not restart");
            Assert((float)tornadoTimer.GetValue(null) == 42f && !(bool)mod.GetType("dda.EveryMapSnow").GetProperty("Excluded", Flags).GetValue(null),
                "grace neither resets tornado timer nor excludes its segment");

            foreach (string name in new[] { "SnowStorm", "WindStorm", "RainStorm" })
            {
                stormRoot.name = name;
                Set(zone, "windActive", true); Set(zone, "localCharacterInsideBounds", true); Set(zone, "observedCharacterInsideBounds", true);
                Set(zone, "windPlayerFactor", 1f); Set(zone, "windIntensity", 1f); Set(zone, "currentForceMult", 1f);
                Set(zone, "hasBeenActiveFor", 12f); Set(zone, "untilSwitch", -1f); statuses = forces = 0;
                update.Invoke(zone, null); physics.Invoke(zone, null);
                zoneType.GetMethod("ApplyStatus", Flags).Invoke(zone, new object[] { character });
                Assert(statuses == 0 && forces == 0, name + " cannot apply status or force during grace: status=" + statuses + ", force=" + forces);
                Assert(!(bool)Get(zone, "windActive") && !(bool)Get(zone, "observedCharacterInsideBounds") &&
                    (float)Get(zone, "windIntensity") == 0 && (float)Get(zone, "currentForceMult") == 0, "clear active/visual/force state");
                rpc.Invoke(zone, new object[] { true, Vector3.forward, 20f });
                Assert(!(bool)Get(zone, "windActive"), "queued native RPC cannot reenable weather");
                visualType.GetMethod("LateUpdate", Flags).Invoke(visual, null);
                Assert(!(bool)Get(visual, "observedPlayerInWindZone") && !particles.isPlaying && (float)Get(visual, "windFactor") == 0,
                    "native visual sees calm weather");
            }
            snowState.SetValue(null, null); snow.GetMethod("Apply", Flags).Invoke(null, new object[] { zone });
            Assert(snowState.GetValue(null) == null, "synthetic snow does not consume its first cycle during grace");
            Assert(!(bool)Get(data, "isInvincible"), "weather grace grants no additional invincibility");
            now = 329.999; Assert(IsBlocked(), "weather held immediately before deadline");
            now = 330; Assert(!IsBlocked(), "deadline releases weather");
            // DayNight is only needed for native visual updates above. Skip effect-specific
            // branches below so the fixture can count the final status delivery boundary.
            dayInstance.SetValue(null, null);
            rpc.Invoke(zone, new object[] { true, Vector3.forward, 20f });
            Assert((bool)Get(zone, "windActive"), "normal host weather RPC resumes after expiry");
            statuses = forces = 0; update.Invoke(zone, null); Set(zone, "untilSwitch", 0f); physics.Invoke(zone, null);
            Assert(statuses == 1 && forces == 1, "native Update and FixedUpdate resume status/force routes");
            snow.GetMethod("Apply", Flags).Invoke(null, new object[] { zone });
            Assert(snowState.GetValue(null) != null && (bool)Get(zone, "windActive"), "synthetic snow starts its normal first cycle after grace");

            resume.SetValue(null, true); now = 900; arm.Invoke(null, new object[] { id }); tick.Invoke(null, null);
            Assert(!Started() && IsBlocked(), "native continuation gets a newly armed weather window");
            resume.SetValue(null, false); runId.SetValue(run, Guid.NewGuid()); tick.Invoke(null, null);
            Assert(!Started(), "old-scene RunId cannot start resumed window");
            runId.SetValue(run, id); now = 1000; tick.Invoke(null, null);
            Assert(Started() && (double)Get(stateField.GetValue(null), "clock") == 1000d, "continuation begins after finalization and readiness");
            foreach (int level in new[] { 0, 9, 19 }) { SetRules(level); Assert(!IsBlocked(), "lower/official levels unchanged"); }
            active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), 0, 1 << 11, true, true));
            Assert(IsBlocked(), "forced ascent 20 uses same weather grace");
            SetRules(20); data.GetType().GetProperty("dead", Flags).SetValue(data, true);
            arm.Invoke(null, new object[] { id }); tick.Invoke(null, null);
            Assert(Started(), "dead or spectating host cannot hold room weather indefinitely");
            reset.Invoke(null, null); Assert(!IsBlocked(), "reset removes stale grace");
            running.SetValue(null, false); arm.Invoke(null, new object[] { Guid.Empty });
            Assert(stateField.GetValue(null) == null, "airport does not arm an unrelated window");
            return checks;

            bool Started() => stateField.GetValue(null) != null && (bool)stateType.GetField("started").GetValue(stateField.GetValue(null));
            bool IsBlocked() => (bool)blocking.GetValue(null);
        }
        finally
        {
            mod.GetType("dda.EffectState").GetMethod("Reset", Flags).Invoke(null, null);
            patches.UnpatchSelf(); observer = null;
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(stormRoot); UnityEngine.Object.DestroyImmediate(particleRoot);
            for (int i = 0; i < savedFields.Length; i++) savedFields[i].SetValue(null, savedValues[i]);
        }
        void SetRules(int level) => active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), level, 0, true, true));
        void Assert(bool pass, string reason) { if (!pass) throw new Exception(reason); checks++; }
    }
    private static object Get(object target, string name) => AccessTools.Field(target.GetType(), name).GetValue(target);
    private static void Set(object target, string name, object value) => AccessTools.Field(target.GetType(), name).SetValue(target, value);
    private static bool Host(ref bool __result) { __result = master; return false; }
    private static bool Clock(ref double __result) { __result = now; return false; }
    private static bool Publish() { publications++; return false; }
    private static bool SkipNetwork() => false;
    private static bool Center(ref Vector3 __result) { __result = Vector3.zero; return false; }
    private static bool Observed(ref object __result) { __result = observer; return false; }
    private static bool Status(bool __runOriginal) { if (__runOriginal) statuses++; return false; }
    private static bool Force() { forces++; return false; }
}
