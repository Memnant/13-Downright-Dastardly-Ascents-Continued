using HarmonyLib;

namespace dda;

[HarmonyPatch(typeof(CharacterAfflictions), "ClearAllStatus")]
internal static class worseClearAll
{
    private static void Prefix(CharacterAfflictions __instance, bool excludeCurse, bool excludePetrify, out float[] __state)
    {
        __state = null;
        if (!Rules.Enabled(12) || !Rules.Snapshot.NerfedRevives) return;
        __state = new float[12];
        for (int i = 0; i < __state.Length; i++)
        {
            var status = (CharacterAfflictions.STATUSTYPE)i;
            if (__instance.StatusIsCurable(status, !excludeCurse, !excludePetrify))
                __state[i] = __instance.GetCurrentStatus(status) * 0.25f;
        }
    }
    private static void Postfix(CharacterAfflictions __instance, float[] __state)
    {
        if (__state == null) return;
        for (int i = 0; i < __state.Length; i++)
            if (__state[i] > 0) __instance.SetStatus((CharacterAfflictions.STATUSTYPE)i, __state[i], false);
        __instance.PushStatuses();
    }
}
