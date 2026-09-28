// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;

namespace dda;

[HarmonyPatch(typeof(Spider), "UpdateAttack")]
internal static class uberSpider
{
	internal static void Prefix(Spider __instance)
	{
		if (!(Rules.Enabled(9))) return;
		if (Rules.Enabled(9))
		{
			EffectState.Set(__instance, "poisonDamage", 0.1f);
			EffectState.Set(__instance, "poisonFrequency", 2f);
		}
	}
}
