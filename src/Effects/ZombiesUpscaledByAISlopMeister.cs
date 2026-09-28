// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;

namespace dda;

[HarmonyPatch(typeof(ZombieManager), "Update")]
internal static class ZombiesUpscaledByAISlopMeister
{
	internal static void Prefix(ZombieManager __instance)
	{
		if (!(Rules.Enabled(9))) return;
		if (Rules.Enabled(9))
		{
			EffectState.Set(__instance, "maxActiveZombies", 10);
		}
	}
}
