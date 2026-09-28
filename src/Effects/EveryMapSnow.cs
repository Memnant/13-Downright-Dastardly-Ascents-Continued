using System;
using HarmonyLib;
using UnityEngine;
using Zorro.Core;

namespace dda;

internal static class EveryMapSnow
{
    private static MapHandler inspectedMap;
    private static WindChillZone nativeSnow;
    internal static WindChillZone Created { get; private set; }
    private static Transform originalParent;
    private static bool originalActive, detached, attempted;
    internal static bool Excluded => Rules.Enabled(20) && MapHandler.Exists &&
        !WeatherPolicy.InWeatherSegment((int)Singleton<MapHandler>.Instance.GetCurrentSegment());
    internal static bool IsCreated(WindChillZone zone) => zone != null && zone == Created;

    internal static void Tick()
    {
        if (!Rules.Enabled(20) || !MapHandler.Exists) return;
        var map = Singleton<MapHandler>.Instance;
        if (!map.hasFinishedStartRoutine) return;
        Ensure(map);
    }

    internal static void Ensure(MapHandler map)
    {
        if (inspectedMap != map)
        {
            Reset(); inspectedMap = map;
            // Once per loaded map, including inactive weather; never scan the scene each frame.
            foreach (var zone in map.GetComponentsInChildren<WindChillZone>(true))
                if (zone.name == "SnowStorm") { nativeSnow = zone; break; }
        }
        if (!Rules.Enabled(20) || Excluded) return;
        if (nativeSnow != null)
        {
            if (!nativeSnow.gameObject.activeInHierarchy && !detached)
            {
                originalParent = nativeSnow.transform.parent; originalActive = nativeSnow.gameObject.activeSelf;
                detached = true;
                nativeSnow.transform.SetParent(map.transform, true);
                nativeSnow.gameObject.SetActive(true);
            }
            allWeather.Apply(nativeSnow);
            return;
        }
        if (Created != null || attempted) return;
        attempted = true;
        try
        {
            Created = CreateInactive(map.transform);
            var previous = WindChillZone.instance;
            Created.gameObject.SetActive(true);
            if (previous != null) WindChillZone.instance = previous;
            Plugin.Log.LogInfo("Ascent 20: added SnowStorm using original Alpine particles, material and fog texture; no extra map loaded.");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("Could not create Continued snow weather: " + e);
        }
    }

    internal static WindChillZone CreateInactive(Transform parent)
    {
        var root = new GameObject("SnowStorm"); root.SetActive(false);
        try
        {
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(0, 580, 841);
            var zone = root.AddComponent<WindChillZone>();
            zone.windZoneBounds = new Bounds(root.transform.position, Vector3.one * 10000f);
            zone.windTimeRangeOn = new Vector2(15, 25); zone.windTimeRangeOff = new Vector2(30, 90);
            zone.lightVolumeSampleThreshold_lower = .15f; zone.lightVolumeSampleThreshold_margin = .15f;
            zone.statusType = CharacterAfflictions.STATUSTYPE.Cold; zone.statusApplicationPerSecond = .07f;
            zone.windForce = 4; zone.forceRadius = 2; zone.delayBeforeForce = 2;
            zone.ragdolledWindForceMult = .5f; zone.windMovesItems = false;
            zone.windIntensityCurve = AnimationCurve.Linear(0, 1, 1, 1);
            zone.disableIfSettingDisabled = (RunSettings.SETTINGTYPE)1900;
            var height = root.AddComponent<WindHeightEffect>();
            height.from = .13f; height.to = .08f; height.fromHeight = 530; height.toHeight = 570;
            var visualObject = new GameObject("StormSphere");
            visualObject.transform.SetParent(root.transform, false); visualObject.tag = "Storm";
            var particles = NativeSnowAssets.InstantiateParticles(visualObject.transform);
            var fog = visualObject.AddComponent<FogConfig>();
            fog.windSkyBrightnessValue = .5f; fog.windTextureInfluence = .3f;
            fog.windTint = new Color(.75f, .75f, .75f, 1); fog.windTexture = NativeSnowAssets.FogTexture;
            fog.windSphereScale = .25f; fog.windSpeed = new Vector2(0, 1.5f); fog.windFogDensity = 50;
            fog.WindFogTextureDensity = 15; fog.maxVal = 1; fog.zone = zone;
            var visual = visualObject.AddComponent<StormVisual>();
            visual.part = new[] { particles }; visual.stormType = StormVisual.StormType.Snow;
            visual.usePhotosensitiveMode = true;
            return zone;
        }
        catch { UnityEngine.Object.Destroy(root); throw; }
    }

    internal static bool Suppress(WindChillZone zone)
    {
        if (WeatherGrace.Suppress(zone)) return true;
        if (!Excluded) return false;
        zone.windActive = false; zone.localCharacterInsideBounds = zone.observedCharacterInsideBounds = false;
        zone.windPlayerFactor = zone.windIntensity = zone.hasBeenActiveFor = 0;
        return true;
    }
    internal static void Reset()
    {
        if (Created != null)
        {
            Created.gameObject.SetActive(false);
            Created.transform.SetParent(null, true); // Do not rediscover a deferred Destroy as native weather.
            UnityEngine.Object.Destroy(Created.gameObject);
        }
        Created = null;
        if (detached && nativeSnow != null && originalParent != null && inspectedMap != null && nativeSnow.transform.parent == inspectedMap.transform)
        {
            nativeSnow.transform.SetParent(originalParent, true); nativeSnow.gameObject.SetActive(originalActive);
        }
        inspectedMap = null; nativeSnow = null; originalParent = null; detached = attempted = false;
    }
}

[HarmonyPatch(typeof(WindChillZone), "HandleTime")]
internal static class ContinuedSnowClock
{
    private static bool Prefix(WindChillZone __instance)
    {
        if (WeatherGrace.Suppress(__instance)) return false;
        if (!EveryMapSnow.IsCreated(__instance)) return true;
        SnowWeatherSync.Apply(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(WindChillZone), "Start")]
internal static class ContinuedSnowStart
{
    private static bool Prefix(WindChillZone __instance) => !EveryMapSnow.IsCreated(__instance);
}

[HarmonyPatch(typeof(WindChillZone), "FixedUpdate")]
internal static class ContinuedWeatherPhysics
{
    private static bool Prefix(WindChillZone __instance) => !EveryMapSnow.Suppress(__instance);
}
