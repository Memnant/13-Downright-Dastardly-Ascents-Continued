// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;

namespace dda;

[HarmonyPatch(typeof(VineShooter), "Update")]
internal static class lessChain
{
	internal static void Prefix(VineShooter __instance)
	{
		if (!(Rules.Enabled(12))) return;
		if (Rules.Enabled(12))
		{
			EffectState.Set(__instance, "maxLength", 30f);
		}
	}
}
