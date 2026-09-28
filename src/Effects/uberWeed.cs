// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;
using UnityEngine;

namespace dda;

[HarmonyPatch(typeof(TumbleWeed), "Start")]
internal static class uberWeed
{
	internal static void Postfix(TumbleWeed __instance)
	{
		if (!(Rules.Enabled(9))) return;
		if (Rules.Enabled(9))
		{
			EffectState.Set(__instance, "rollForce", EffectState.Original(__instance, "weed.roll", __instance.rollForce) * Mathf.Lerp(0.5f, 1f, Mathf.Pow(Random.value, 6f)));
			if (Rules.Enabled(9))
			{
				EffectState.Set(__instance, "powerMultiplier", EffectState.Original(__instance, "weed.power", __instance.powerMultiplier) * 2f);
			}
		}
	}
}
