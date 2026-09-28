// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;
using Peak.Afflictions;

namespace dda;

[HarmonyPatch(typeof(Affliction_FasterBoi), "OnApplied")]
internal static class worseNRGDrink
{
	internal static void Prefix(Affliction_FasterBoi __instance)
	{
		if (!(Rules.Enabled(12))) return;
		if (Rules.Enabled(12))
		{
			EffectState.Set(__instance, "moveSpeedMod", 0.6f);
			EffectState.Set(__instance, "climbSpeedMod", 0.7f);
		}
	}
}
