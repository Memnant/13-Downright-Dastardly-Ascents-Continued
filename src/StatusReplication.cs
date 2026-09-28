using System;
using HarmonyLib;

namespace dda;

// SetAll receives absolute owner state. These values already include the owner's penalties.
// ApplyStatusesFromFloatArray, in contrast, is a new effect and still uses the normal patches.
[HarmonyPatch(typeof(CharacterAfflictions), "SetAll")]
internal static class StatusReplication
{
    [ThreadStatic] private static int depth;
    internal static bool Active => depth > 0;
    private static void Prefix(out bool __state)
    {
        __state = Rules.Enabled(9) || Rules.Enabled(12) || Rules.Enabled(17);
        if (__state) depth++;
    }
    private static void Postfix(CharacterAfflictions __instance, float[] statuses, bool __state)
    {
        if (!__state || statuses == null || statuses.Length != __instance.currentStatuses.Length) return;
        // Restore exact replicated legacy values after native UI/effect processing. In particular,
        // native skeleton immunity must not discard the quarter-strength statuses the owner accepted.
        for (int i = 0; i < Math.Min(12, statuses.Length); i++)
        {
            if (float.IsNaN(statuses[i]) || float.IsInfinity(statuses[i]) || statuses[i] < 0f) continue;
            __instance.currentStatuses[i] = statuses[i];
            __instance.currentIncrementalStatuses[i] = 0f;
            __instance.currentDecrementalStatuses[i] = 0f;
        }
    }
    private static Exception Finalizer(Exception __exception, bool __state)
    {
        if (__state) depth--;
        return __exception;
    }
}
