using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class EveryMapSnowRegression
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    internal static int Run(Assembly mod)
    {
        var coordinator = mod.GetType("dda.RunCoordinator");
        var active = coordinator.GetField("Active", Flags); var running = coordinator.GetField("InRun", Flags);
        var mapType = AccessTools.TypeByName("MapHandler");
        var mapInstance = mapType.BaseType.GetField("_instance", Flags);
        var zoneType = AccessTools.TypeByName("WindChillZone");
        var snow = mod.GetType("dda.EveryMapSnow");
        var ensure = snow.GetMethod("Ensure", Flags); var reset = snow.GetMethod("Reset", Flags);
        var created = snow.GetProperty("Created", Flags);
        var effectReset = mod.GetType("dda.EffectState").GetMethod("Reset", Flags);
        object oldActive = active.GetValue(null), oldRunning = running.GetValue(null), oldMap = mapInstance.GetValue(null);
        var root = new GameObject("Continued map without Alpine"); root.SetActive(false);
        GameObject generatedRoot = null;
        int checks = 0;
        try
        {
            int sceneCount = SceneManager.sceneCount;
            var map = root.AddComponent(mapType); mapInstance.SetValue(null, map);
            var segment = mapType.GetField("currentSegment", Flags);
            segment.SetValue(map, 0); running.SetValue(null, true);
            active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), 19, 0, true, true));
            ensure.Invoke(null, new object[] { map });
            Assert(created.GetValue(null) == null, "level 19 does not create snow");
            active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), 20, 0, true, true));
            ensure.Invoke(null, new object[] { map });
            var zone = (Component)created.GetValue(null);
            Assert(zone != null, "map without Alpine gets the original Alpine snow asset");
            generatedRoot = zone.gameObject;
            for (int i = 0; i < 4; i++) ensure.Invoke(null, new object[] { map });
            Assert(root.GetComponentsInChildren(zoneType, true).Length == 1, "repeated initialization creates only one snow zone");
            Assert(SceneManager.sceneCount == sceneCount, "creating snow does not load another scene");
            Assert(zone.GetComponent(AccessTools.TypeByName("Photon.Pun.PhotonView")) == null, "generated snow does not use an unregistered Photon view");
            var particles = zone.GetComponentInChildren<ParticleSystem>(true);
            var material = particles.GetComponent<ParticleSystemRenderer>().sharedMaterial;
            Assert(material != null && material.name == "M_VFX_Snow", "original Alpine material replaces Snowball material");
            Assert(material.shader != null && material.shader.name == "Storm", "original compiled Storm shader resolves");
            Assert(material.GetTexture("_Texture")?.name == "A_Texture 37", "original snow sheet texture resolves");
            Assert(material.GetTextureScale("_Texture") == new Vector2(8, 8), "native snow texture tiling preserved");
            Assert(particles.main.maxParticles == 1000 && particles.main.playOnAwake, "native particle capacity and playback settings");
            Assert(particles.main.simulationSpeed == 15 && particles.main.startLifetime.constant == 3, "native simulation speed and lifetime");
            Assert(particles.main.startSize.constantMin == 10 && particles.main.startSize.constantMax == 15, "native snow-sheet size preserved");
            Assert(particles.emission.rateOverTime.constant == 300, "native emission rate preserved");
            Assert(Math.Abs(particles.transform.localPosition.z + 14.38f) < .0001f, "native particle offset preserved");
            var fog = zone.GetComponentInChildren(AccessTools.TypeByName("FogConfig"), true);
            Assert(((Texture)fog.GetType().GetField("windTexture", Flags).GetValue(fog)).name == "A_Texture 23", "original storm fog texture replaces grey placeholder");
            Assert((Vector2)zoneType.GetField("windTimeRangeOn", Flags).GetValue(zone) == new Vector2(15, 25) &&
                (Vector2)zoneType.GetField("windTimeRangeOff", Flags).GetValue(zone) == new Vector2(30, 90), "snow keeps native active duration and uses the requested 30-90 second rest");
            var visualType = AccessTools.TypeByName("StormVisual");
            var visual = zone.GetComponentInChildren(visualType, true);
            Assert(visual != null && visual.gameObject.CompareTag("Storm"), "native visual and scout storm-audio binding are present");
            Assert((bool)visualType.GetField("usePhotosensitiveMode", Flags).GetValue(visual), "photosensitive setting remains supported");
            var physics = mod.GetType("dda.ContinuedWeatherPhysics").GetMethod("Prefix", Flags);
            var update = mod.GetType("dda.ContinuedWeatherUpdate").GetMethod("Prefix", Flags);
            foreach (int stage in new[] { 0, 1, 2, 3, 4, 5, 6 })
            {
                segment.SetValue(map, stage);
                foreach (string name in new[] { "SnowStorm", "RainStorm", "WindStorm" })
                {
                    zone.gameObject.name = name;
                    zoneType.GetField("windActive", Flags).SetValue(zone, true);
                    zoneType.GetField("localCharacterInsideBounds", Flags).SetValue(zone, true);
                    zoneType.GetField("observedCharacterInsideBounds", Flags).SetValue(zone, true);
                    Assert((bool)update.Invoke(null, new[] { zone }) == (stage < 4), "weather status gate for " + name + "/" + stage);
                    Assert((bool)physics.Invoke(null, new[] { zone }) == (stage < 4), "weather physics gate for " + name + "/" + stage);
                    if (stage >= 4) Assert(!(bool)zoneType.GetField("windActive", Flags).GetValue(zone) &&
                        !(bool)zoneType.GetField("observedCharacterInsideBounds", Flags).GetValue(zone), "final segment clears storm and visual flags");
                }
            }
            active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), 0, 0, true, true));
            zoneType.GetField("windActive", Flags).SetValue(zone, true);
            Assert((bool)update.Invoke(null, new[] { zone }) && (bool)zoneType.GetField("windActive", Flags).GetValue(zone), "official weather is not suppressed");
            reset.Invoke(null, null); UnityEngine.Object.DestroyImmediate(generatedRoot); generatedRoot = null;
            Assert(root.GetComponentsInChildren(zoneType, true).Length == 0, "reset removes generated weather");
            ensure.Invoke(null, new object[] { map });
            Assert(created.GetValue(null) == null, "official mode does not recreate snow after reset");
            active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), 20, 0, true, true));
            segment.SetValue(map, 4); ensure.Invoke(null, new object[] { map });
            Assert(created.GetValue(null) == null, "continuing at Citadel/Kiln does not create snow");

            reset.Invoke(null, null);
            var alpine = new GameObject("Alpine"); alpine.transform.SetParent(root.transform, false); alpine.SetActive(false);
            var existing = new GameObject("SnowStorm"); existing.transform.SetParent(alpine.transform, false);
            var original = existing.AddComponent(zoneType);
            segment.SetValue(map, 0); ensure.Invoke(null, new object[] { map });
            Assert(created.GetValue(null) == null && root.GetComponentsInChildren(zoneType, true).Length == 1, "native SnowStorm is reused without duplicate");
            Assert(existing.transform.parent == map.transform && !alpine.activeSelf, "enable only weather without enabling an inactive biome");
            reset.Invoke(null, null);
            Assert(existing.transform.parent == alpine.transform, "reset restores native weather parent");

            var stateType = mod.GetType("dda.SnowWeatherState");
            var state = Activator.CreateInstance(stateType);
            stateType.GetField("runId").SetValue(state, Guid.NewGuid().ToString("N"));
            stateType.GetField("clock").SetValue(state, 100d); stateType.GetField("duration").SetValue(state, 20f);
            stateType.GetField("active").SetValue(state, true); stateType.GetField("direction").SetValue(state, -1);
            var apply = mod.GetType("dda.SnowWeatherSync").GetMethod("ApplyState", Flags);
            apply.Invoke(null, new[] { original, state, (object)108d });
            Assert((bool)zoneType.GetField("windActive", Flags).GetValue(original) && (float)zoneType.GetField("untilSwitch", Flags).GetValue(original) == 12f, "late client joins current snow phase");
            var heading = (Vector3)zoneType.GetField("currentWindDirection", Flags).GetValue(original);
            Assert(heading.x < 0 && heading.z > 0 && Math.Abs(heading.magnitude - 1f) < .00001f, "shared wind direction normalized");
            apply.Invoke(null, new[] { original, state, (object)121d });
            Assert(!(bool)zoneType.GetField("windActive", Flags).GetValue(original), "expired active snapshot stops rather than running forever");
            apply.Invoke(null, new[] { original, null, (object)108d });
            Assert(!(bool)zoneType.GetField("windActive", Flags).GetValue(original), "wait for host before applying generated snow");
            var tornadoObject = new GameObject("Continued terminal weather fixture");
            tornadoObject.SetActive(false); tornadoObject.transform.SetParent(root.transform, false);
            var viewType = AccessTools.TypeByName("Photon.Pun.PhotonView");
            var view = tornadoObject.AddComponent(viewType);
            var tornado = tornadoObject.AddComponent(AccessTools.TypeByName("Tornado"));
            var instantiationData = viewType.GetProperty("InstantiationData", Flags);
            var stopTornado = mod.GetType("dda.ContinuedFinalSegmentTornado").GetMethod("Prefix", Flags);
            var stopTornadoPhysics = mod.GetType("dda.ContinuedFinalSegmentTornadoPhysics").GetMethod("Prefix", Flags);
            foreach (bool ours in new[] { false, true })
            foreach (int stage in new[] { 3, 4 })
            {
                instantiationData.SetValue(view, ours ? new object[] { "dda.continued.tornado.v1" } : null);
                segment.SetValue(map, stage);
                bool expected = !ours || stage < 4;
                Assert((bool)stopTornado.Invoke(null, new[] { tornado }) == expected, "only extra tornado stops at final segment");
                Assert((bool)stopTornadoPhysics.Invoke(null, new[] { tornado }) == expected, "tornado capture/forces are blocked before destruction");
            }
            return checks;
        }
        finally
        {
            reset.Invoke(null, null); effectReset.Invoke(null, null);
            if (generatedRoot != null) UnityEngine.Object.DestroyImmediate(generatedRoot);
            active.SetValue(null, oldActive); running.SetValue(null, oldRunning); mapInstance.SetValue(null, oldMap);
            UnityEngine.Object.DestroyImmediate(root);
            mod.GetType("dda.NativeSnowAssets").GetMethod("Release", Flags).Invoke(null, null);
        }
        void Assert(bool pass, string reason) { if (!pass) throw new Exception(reason); checks++; }
    }
}
