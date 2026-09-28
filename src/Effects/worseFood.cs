// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;

namespace dda;

[HarmonyPatch(typeof(Action_RestoreHunger), "RunAction")]
internal static class worseFood
{
	internal static bool Prefix(Action_RestoreHunger __instance)
	{
		if (!(Rules.Enabled(12))) return true;
		if (Rules.Enabled(12))
		{
			((ItemActionBase)__instance).character.refs.afflictions.SubtractStatus(CharacterAfflictions.STATUSTYPE.Hunger, __instance.restorationAmount * 0.5f);
		}
		else
		{
			((ItemActionBase)__instance).character.refs.afflictions.SubtractStatus(CharacterAfflictions.STATUSTYPE.Hunger, __instance.restorationAmount);
		}
		return false;
	}
}
