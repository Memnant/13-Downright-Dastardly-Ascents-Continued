using System;
using HarmonyLib;

namespace dda;

// Keep the current AddStatus implementation, including petrification, caps and ownership.
[HarmonyPatch(typeof(CharacterAfflictions), "AddStatus")]
internal static class worseIdol
{
    [ThreadStatic] private static int skeletonStatusScope;
    internal static bool InSkeletonScope => skeletonStatusScope > 0;
    private static void Prefix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType,
        ref float amount, ref bool ignoreInvincibility, out bool __state)
    {
        __state = false;
        if (!Rules.Enabled(12) || StatusReplication.Active || !Rules.Owns(__instance.character) || (int)statusType > 11) return;
        var character = __instance.character;
        if (character.data.isSkeleton)
        {
            if (!__instance.StatusAffectsSkeleton(statusType)) amount *= 0.25f;
            if (statusType == CharacterAfflictions.STATUSTYPE.Injury && !character.data.isInFog) amount *= 99f;
            skeletonStatusScope++;
            __state = true;
        }
        if (!OpeningProtection.Protects(character) && !ignoreInvincibility && character.data.isInvincible && statusType != CharacterAfflictions.STATUSTYPE.Curse)
        {
            amount *= 0.25f;
            ignoreInvincibility = true;
        }
    }
    private static Exception Finalizer(Exception __exception, bool __state)
    {
        if (__state) skeletonStatusScope--;
        return __exception;
    }
}

[HarmonyPatch(typeof(CharacterAfflictions), "StatusAffectsSkeleton")]
internal static class ContinuedSkeletonStatuses
{
    private static void Postfix(CharacterAfflictions.STATUSTYPE type, ref bool __result)
    {
        if (Rules.Enabled(12) && worseIdol.InSkeletonScope && (int)type <= 11) __result = true;
    }
}
