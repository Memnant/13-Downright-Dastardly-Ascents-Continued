using System;
using HarmonyLib;
using UnityEngine;
using Zorro.Core;

namespace dda;

[HarmonyPatch(typeof(CharacterAfflictions), "UpdateNormalStatuses")]
internal static class uniqueAilments
{
    private const float SlowRecoveryPerSecond = 0.00333333f;
    internal struct Rates { internal bool changed; internal float drowsy, hot; }
    private static void Prefix(CharacterAfflictions __instance, out Rates __state)
    {
        __state = default;
        var c = __instance.character;
        if (!Rules.Owns(c) || (!Rules.Enabled(16) && !Rules.Enabled(18))) return;
        __state = new Rates { changed = true, drowsy = __instance.drowsyReductionPerSecond, hot = __instance.hotReductionPerSecond };
        bool slowDrowsy = false;
        float regionalDrowsyRate = SlowRecoveryPerSecond;
        if (Rules.Enabled(18) && MapHandler.Exists)
        {
            var map = Singleton<MapHandler>.Instance;
            var biome = map.GetCurrentBiome();
            var segment = map.GetCurrentSegment();
            if (biome == Biome.BiomeType.Volcano && (segment == Segment.Caldera || segment == Segment.TheKiln))
                __instance.hotReductionPerSecond = SlowRecoveryPerSecond;
            // PEAK 2.4.c serializes Temple_Segment as Swamp; the segment separates
            // it from the swamp. Also accept an explicitly labeled Temple in that slot.
            slowDrowsy = (biome == Biome.BiomeType.Swamp && (segment == Segment.Caldera || segment == Segment.TheKiln)) ||
                (biome == Biome.BiomeType.Temple && segment == Segment.TheKiln);
            if (slowDrowsy)
            {
                if (Tier20Swamp.Active) regionalDrowsyRate = Tier20Swamp.DayRecoveryPerSecond;
                __instance.drowsyReductionPerSecond = regionalDrowsyRate;
            }
        }
        if (!Rules.Enabled(16)) return;
        float poison = __instance.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Poison);
        float spores = __instance.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Spores);
        if ((poison > 0.02f || spores > 0.02f) && UnityEngine.Random.Range((poison + spores) * 2000f * Time.deltaTime, 2000f * Time.deltaTime) > 1999f * Time.deltaTime)
            c.Fall(0.1f, 0f);
        if (__instance.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Injury) > 0.02f)
            __instance.AddStatus(CharacterAfflictions.STATUSTYPE.Injury, 0.00033333f * Time.deltaTime);
        float weight = __instance.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Weight);
        if (weight > 0 && !c.data.isClimbing && c.data.groundedFor < 0.5f)
            c.GetBodypart(BodypartType.Hip).rig.linearVelocity -= Vector3.up * weight * 100f * Time.deltaTime;
        if (DayNightManager.instance != null && DayNightManager.instance.isDay < 0.5f)
            __instance.drowsyReductionPerSecond = 0f;
        else if (DayNightManager.instance != null) __instance.drowsyReductionPerSecond = slowDrowsy ? regionalDrowsyRate : 0.02f;
        float hot = __instance.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Hot);
        if (hot > 0 && (c.data.isClimbing || c.data.isSprinting)) c.AddStamina(-hot * 0.15f * Time.deltaTime);
        // Cold's slowdown is applied at the movement/climbing calls, without persistent accumulation.
    }
    private static Exception Finalizer(CharacterAfflictions __instance, Rates __state, Exception __exception)
    {
        if (__state.changed)
        {
            __instance.drowsyReductionPerSecond = __state.drowsy;
            __instance.hotReductionPerSecond = __state.hot;
        }
        return __exception;
    }
}

[HarmonyPatch(typeof(CharacterMovement), "GetMovementForce")]
internal static class ContinuedColdMovement
{
    private static void Postfix(CharacterMovement __instance, ref float __result)
    {
        if (Rules.Enabled(16) && Rules.Owns(__instance.character))
            __result *= 1f - __instance.character.refs.afflictions.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Cold) * 0.35f;
    }
}

[HarmonyPatch(typeof(CharacterClimbing), "GetRequestedPostition")]
internal static class ContinuedColdClimbing
{
    private static void Prefix(CharacterClimbing __instance, out float? __state)
    {
        __state = null;
        if (!Rules.Enabled(16) || !Rules.Owns(__instance.character)) return;
        __state = __instance.climbSpeedMod;
        __instance.climbSpeedMod *= 1f - __instance.character.refs.afflictions.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Cold) * 0.35f;
    }
    private static Exception Finalizer(CharacterClimbing __instance, float? __state, Exception __exception)
    {
        if (__state.HasValue) __instance.climbSpeedMod = __state.Value;
        return __exception;
    }
}
