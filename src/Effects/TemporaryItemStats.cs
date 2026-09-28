using System;
using HarmonyLib;

namespace dda;

[HarmonyPatch(typeof(RopeShooter), "OnPrimaryFinishedCast")]
internal static class lessRope
{
    private static void Prefix(RopeShooter __instance, out float? __state)
    {
        __state = null;
        if (!Rules.Enabled(12)) return;
        __state = __instance.length;
        __instance.length *= 0.75f;
    }
    private static Exception Finalizer(RopeShooter __instance, float? __state, Exception __exception)
    {
        if (__state.HasValue) __instance.length = __state.Value;
        return __exception;
    }
}

[HarmonyPatch(typeof(Beetle), "InflictAttack")]
internal static class uberBeetle
{
    internal struct Stats { internal bool changed; internal float force, range, ragdoll; }
    private static void Prefix(Beetle __instance, out Stats __state)
    {
        __state = default;
        if (!Rules.Enabled(9)) return;
        __state = new Stats { changed = true, force = __instance.bonkForce, range = __instance.bonkRange, ragdoll = __instance.ragdollTime };
        __instance.bonkForce *= 10f;
        __instance.bonkRange += 2f;
        __instance.ragdollTime = 10f;
    }
    private static Exception Finalizer(Beetle __instance, Stats __state, Exception __exception)
    {
        if (__state.changed)
        {
            __instance.bonkForce = __state.force;
            __instance.bonkRange = __state.range;
            __instance.ragdollTime = __state.ragdoll;
        }
        return __exception;
    }
}

[HarmonyPatch(typeof(Action_ModifyStatus), "RunAction")]
internal static class worseMedkit
{
    private static void Prefix(Action_ModifyStatus __instance, out float? __state)
    {
        __state = null;
        // Petrification is a separate integer-valued system added after the original mod.
        // Item costs such as Scout's Ambition must retain their native 30 points.
        if (!Rules.Enabled(12) || __instance.statusType == CharacterAfflictions.STATUSTYPE.Petrify) return;
        __state = __instance.changeAmount;
        __instance.changeAmount = (__instance.changeAmount > 1f ? 0.7f : __instance.changeAmount) * 0.6f;
    }
    private static Exception Finalizer(Action_ModifyStatus __instance, float? __state, Exception __exception)
    {
        if (__state.HasValue) __instance.changeAmount = __state.Value;
        return __exception;
    }
}
