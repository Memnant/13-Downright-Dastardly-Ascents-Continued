using System;
using HarmonyLib;
using UnityEngine;

namespace dda;

internal static class LegacyPenalties
{
    internal static void MakeDrowsy(Character character, float threshold)
    {
        if (!Rules.Owns(character)) return;
        var afflictions = character.refs.afflictions;
        float sum = StatusSum(afflictions);
        if (sum < threshold) afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Drowsy, threshold - sum);
    }

    internal static float StatusSum(CharacterAfflictions afflictions) =>
            afflictions.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Poison)
            + afflictions.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Drowsy)
            + afflictions.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Injury)
            + afflictions.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Hunger)
            + afflictions.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Cold)
            + afflictions.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Hot);

    internal static void ExplosionDrowsiness(Character character)
    {
        if (!Rules.Owns(character)) return;
        var afflictions = character.refs.afflictions;
        // Preserve the original 0.01 steps and 1.02 threshold, including cumulative multipliers.
        // Explicit immunity/cap exits and a hard bound prevent the original infinite loop.
        for (int step = 0; step < 4096 && StatusSum(afflictions) < 1.02f; step++)
        {
            if (afflictions.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Drowsy) >=
                afflictions.GetStatusCap(CharacterAfflictions.STATUSTYPE.Drowsy)) break;
            if (!afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Drowsy, 0.01f)) break;
        }
    }
}

internal static class FallPenaltyScope
{
    internal struct State { internal Character previous; internal bool previousDamage; internal bool entered; }
    [ThreadStatic] internal static Character Character;
    [ThreadStatic] internal static bool TookDamage;
    internal static State Enter(Character character)
    {
        var state = new State { previous = Character, previousDamage = TookDamage, entered = Rules.Enabled(13) && Rules.Owns(character) };
        if (state.entered) { Character = character; TookDamage = false; }
        return state;
    }
    internal static Exception Exit(State state, Exception error)
    {
        if (!state.entered) return error;
        var character = Character;
        bool damage = TookDamage;
        Character = state.previous;
        TookDamage = state.previousDamage;
        if (error == null && damage) LegacyPenalties.MakeDrowsy(character, 1.04f);
        return error;
    }
}

[HarmonyPatch(typeof(CharacterClimbing), "CheckFallDamage")]
internal static class sleepyFall
{
    private static void Prefix(CharacterClimbing __instance, out FallPenaltyScope.State __state) => __state = FallPenaltyScope.Enter(__instance.character);
    private static Exception Finalizer(Exception __exception, FallPenaltyScope.State __state) => FallPenaltyScope.Exit(__state, __exception);
}

[HarmonyPatch(typeof(CharacterMovement), "CheckFallDamage")]
internal static class sleepyFall2
{
    private static void Prefix(CharacterMovement __instance, out FallPenaltyScope.State __state) => __state = FallPenaltyScope.Enter(__instance.character);
    private static Exception Finalizer(Exception __exception, FallPenaltyScope.State __state) => FallPenaltyScope.Exit(__state, __exception);
}

[HarmonyPatch(typeof(CharacterAfflictions), "AddStatus")]
internal static class ObserveFallInjury
{
    private static void Postfix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, float amount, bool __result)
    {
        if (__result && amount > 0 && statusType == CharacterAfflictions.STATUSTYPE.Injury &&
            __instance.character == FallPenaltyScope.Character) FallPenaltyScope.TookDamage = true;
    }
}

[HarmonyPatch(typeof(Bodypart), "SnapToAnim")]
internal static class NoFallCancel
{
    internal struct Motion { internal Rigidbody rig; internal Vector3 linear, angular; }
    private static void Prefix(Bodypart __instance, out Motion __state)
    {
        __state = default;
        if (!Rules.Enabled(13)) return;
        var character = __instance.character != null ? __instance.character : __instance.GetComponentInParent<Character>();
        if (TornadoRecovery.AllowsSnap(character)) return;
        var rig = __instance.GetComponent<Rigidbody>();
        if (rig == null || rig.isKinematic) return;
        __state = new Motion { rig = rig, linear = rig.linearVelocity, angular = rig.angularVelocity };
    }
    private static void Postfix(Motion __state)
    {
        if (__state.rig == null || __state.rig.isKinematic) return;
        __state.rig.linearVelocity = __state.linear;
        __state.rig.angularVelocity = __state.angular;
    }
}
