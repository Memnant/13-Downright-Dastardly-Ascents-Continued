// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;

namespace dda;

[HarmonyPatch(typeof(StatusTrigger), "OnTriggerEnter")]
internal static class poison
{
	internal static bool Prefix(StatusTrigger __instance)
	{
		if (!(Rules.Enabled(9))) return true;
		if (Rules.Enabled(9))
		{
			EffectState.Set(__instance, "poisonOverTimeDuration", 999f);
			EffectState.Set(__instance, "statusAmount", 0.15f);
			EffectState.Set(__instance, "poisonOverTimeAmountPerSecond", 0.1f);
		}
		return true;
	}
}
