// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;

namespace dda;

[HarmonyPatch(typeof(Action_GiveExtraStamina), "RunAction")]
internal static class worseXtraStam
{
	internal static bool Prefix(Action_GiveExtraStamina __instance)
	{
		if (!(Rules.Enabled(12))) return true;
		if (Rules.Enabled(12))
		{
			((ItemActionBase)__instance).character.AddExtraStamina(__instance.amount * 0.5f);
		}
		else
		{
			((ItemActionBase)__instance).character.AddExtraStamina(__instance.amount);
		}
		return false;
	}
}
