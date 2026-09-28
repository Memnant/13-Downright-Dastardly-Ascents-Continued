// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;

namespace dda;

[HarmonyPatch(typeof(RescueHook), "RPCA_LetGo")]
internal static class worseHook
{
	internal static void Prefix(RescueHook __instance)
	{
		if (!(Rules.Enabled(12))) return;
		if (Rules.Enabled(12))
		{
			if (Rules.Owns(__instance.targetPlayer))
			{
				__instance.targetPlayer.Fall(2f, 0f);
				__instance.targetPlayer.refs.items.DropAllItems(true);
			}
			else if (__instance.targetPlayer == null && Rules.Owns(__instance.playerHoldingItem))
			{
				__instance.playerHoldingItem.Fall(2f, 0f);
				__instance.playerHoldingItem.refs.items.DropAllItems(true);
			}
		}
	}
}
