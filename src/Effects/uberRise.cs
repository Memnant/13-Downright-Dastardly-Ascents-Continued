using HarmonyLib;

namespace dda;

[HarmonyPatch(typeof(Lava), "FixedUpdate")]
internal static class uberRise
{
    private static bool Prefix(Lava __instance) => TideSync.Apply(__instance);
}

[HarmonyPatch(typeof(Lava), "Update")]
internal static class TideHeatPosition
{
    private static bool Prefix(Lava __instance) => TideSync.Apply(__instance);
}
