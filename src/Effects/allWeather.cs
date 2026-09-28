// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace dda;

[HarmonyPatch(typeof(WindChillZone), "Awake")]
internal static class allWeather
{
    private static ConditionalWeakTable<WindChillZone, object> applied = new();
    private static readonly object marker = new();

    // Awake sets the world-space center. Capture its finished Bounds, not the serialized value.
    internal static void Postfix(WindChillZone __instance) => Apply(__instance);

    internal static void Apply(WindChillZone zone)
    {
        if (!Rules.Enabled(20) || applied.TryGetValue(zone, out _)) return;
        // Bounds is a struct. Mutating a boxed copy of Bounds.size never changes the zone.
        Bounds bounds = zone.windZoneBounds;
        bounds.size += Vector3.one * 3000f;
        EffectState.Set(zone, nameof(WindChillZone.windZoneBounds), bounds);
        EffectState.Set(zone, nameof(WindChillZone.setSlippy), true);
        if (zone.name == "WindStorm" || zone.name == "SnowStorm")
            EffectState.Set(zone, nameof(WindChillZone.windTimeRangeOff), new Vector2(30f, 90f));
        applied.Add(zone, marker);
        // Let the native host clock/RPC start and stop the storm. Forcing windActive=true
        // with an uninitialized timer made its first tick immediately switch it off.
        Plugin.Log.LogInfo("Ascent 20 weather coverage applied: " + zone.name + "; size=" + bounds.size);
    }

    internal static void Reset() => applied = new ConditionalWeakTable<WindChillZone, object>();
}

// Joining/restoring can deliver the room rules after scene Awake. Check once per zone
// once they arrive; already-applied frames do not scan objects or use reflection.
[HarmonyPatch(typeof(WindChillZone), "Update")]
internal static class ContinuedWeatherUpdate
{
    internal static bool Prefix(WindChillZone __instance)
    {
        allWeather.Apply(__instance);
        return !EveryMapSnow.Suppress(__instance);
    }
}

// Native snow can follow the team across segments. Choose the current region's
// interval only when the host starts a new rest; never restart an existing phase.
[HarmonyPatch(typeof(WindChillZone), "GetNextWindTime")]
internal static class ContinuedSwampSnowRest
{
    private static void Prefix(WindChillZone __instance, bool windActive, out Vector2? __state)
    {
        __state = null;
        if (windActive || __instance.name != "SnowStorm" || !Tier20Swamp.Active) return;
        __state = __instance.windTimeRangeOff;
        __instance.windTimeRangeOff = Tier20Swamp.SnowRestRange;
    }
    private static Exception Finalizer(WindChillZone __instance, Vector2? __state, Exception __exception)
    {
        if (__state.HasValue) __instance.windTimeRangeOff = __state.Value;
        return __exception;
    }
}
