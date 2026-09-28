using System;
using HarmonyLib;
using UnityEngine;
using Zorro.Core;

namespace dda;

internal static class ChasingFog
{
    internal static bool Active(OrbFogHandler handler)
    {
        if (!Rules.Enabled(18) || !Ascents.fogEnabled || handler == null || handler.sphere == null ||
            handler.origins == null || handler.currentID < 0 || handler.currentID >= handler.origins.Length ||
            handler.origins[handler.currentID] == null || !MapHandler.Exists) return false;
        var map = Singleton<MapHandler>.Instance;
        int segment = (int)map.GetCurrentSegment();
        if (segment < 0 || segment >= (int)Segment.TheKiln) return false;
        if (handler.currentID < (int)Segment.Caldera) return !handler.origins[handler.currentID].disableFog;
        return VolcanoExtension(handler) || SwampChasingFog.Available(handler);
    }

    internal static bool VolcanoExtension(OrbFogHandler handler)
    {
        if (handler.currentID != (int)Segment.Caldera || !MapHandler.Exists) return false;
        var map = Singleton<MapHandler>.Instance;
        return map.GetCurrentSegment() == Segment.Caldera && map.segments != null &&
            map.currentSegment >= 0 && map.currentSegment < map.segments.Length &&
            map.segments[map.currentSegment] != null && map.GetCurrentBiome() == Biome.BiomeType.Volcano;
    }
}

[HarmonyPatch(typeof(OrbFogHandler), "Move")]
internal static class Tier18FogSpeed
{
    internal struct State { internal bool changed; internal float original, applied; }
    private static void Prefix(OrbFogHandler __instance, out State __state)
    {
        __state = default;
        if (!ChasingFog.Active(__instance) || __instance.speed <= 0 || __instance.speed > .4f) return;
        // Preserve the old <= 0.4 guard, but apply only for this native movement call.
        // Repeated origin initialization must never multiply a stored speed again.
        __state = new State { changed = true, original = __instance.speed, applied = __instance.speed * 2f };
        __instance.speed = __state.applied;
    }
    private static Exception Finalizer(OrbFogHandler __instance, State __state, Exception __exception)
    {
        if (__state.changed && __instance.speed == __state.applied) __instance.speed = __state.original;
        return __exception;
    }
}

[HarmonyPatch(typeof(OrbFogHandler), "TimeToMove")]
internal static class Tier18FogWait
{
    private static void Postfix(OrbFogHandler __instance, ref bool __result)
    {
        // Old TimeToMoveFix actually used > 20 seconds, despite the "instant" description.
        // Native WaitToMove still handles resting, earlier player progress and host RPCs.
        if (ChasingFog.Active(__instance)) __result = __instance.currentWaitTime > 20f;
    }
}

[HarmonyPatch(typeof(OrbFogHandler), "InitNewSphere")]
internal static class Tier18VolcanoFogOrigin
{
    private static void Postfix(OrbFogHandler __instance)
    {
        SwampChasingFog.ForgetOrigin(__instance);
        if (!ChasingFog.Active(__instance)) return;
        if (SwampChasingFog.ActiveRegion) { SwampChasingFog.Initialize(__instance); return; }
        if (!ChasingFog.VolcanoExtension(__instance)) return;
        // Original nonStop's Caldera geometry. Native SetFogOrigin/RPC_InitFog retain
        // authority over stage changes and overwrite the size with the host snapshot.
        __instance.sphere.fogPoint = new Vector3(-3.73f, 862.99f, 1960.66f);
        __instance.currentSize = 790f;
        __instance.currentStartHeight = 0f;
        __instance.currentStartForward = 0f;
    }
}

[HarmonyPatch(typeof(OrbFogHandler), "Update")]
internal static class Tier18VolcanoFogUpdate
{
    internal struct State { internal FogSphereOrigin origin; internal bool disabled; }
    private static void Prefix(OrbFogHandler __instance, out State __state)
    {
        __state = default;
        SwampChasingFog.EnsureGeometry(__instance);
        if (!ChasingFog.Active(__instance) || __instance.currentID != (int)Segment.Caldera) return;
        var origin = __instance.origins[__instance.currentID];
        __state = new State { origin = origin, disabled = origin.disableFog };
        origin.disableFog = false;
    }
    private static Exception Finalizer(State __state, Exception __exception)
    {
        if (__state.origin != null) __state.origin.disableFog = __state.disabled;
        return __exception;
    }
}
