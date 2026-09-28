// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;
using Peak.Afflictions;

namespace dda;

[HarmonyPatch(typeof(Action_InflictPoison), "RunAction")]
internal static class Action_InflictPoisonPatch
{
	internal static bool Prefix(Action_InflictPoison __instance)
	{
		if (!(Rules.Enabled(12))) return true;
		if (Rules.Enabled(12))
		{
			((ItemActionBase)__instance).character.refs.afflictions.AddAffliction(new Affliction_PoisonOverTime(__instance.inflictionTime * 2f, __instance.delay * 3f, __instance.poisonPerSecond * 50f));
		}
		return true;
	}
}
