// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;

namespace dda;

[HarmonyPatch(typeof(Action_ReduceUses), "ReduceUsesRPC")]
internal static class lessUse
{
	internal static bool Prefix(Action_ReduceUses __instance)
	{
		if (!(Rules.Enabled(12))) return true;
		OptionableIntItemData data = ((ItemActionBase)__instance).item.GetData<OptionableIntItemData>(DataEntryKey.ItemUses);
		if (data.HasData && data.Value > 0)
		{
			if ((Rules.Enabled(12)) && data.Value >= 2)
			{
				data.Value--;
			}
			data.Value--;
			if (((ItemActionBase)__instance).item.totalUses > 0)
			{
				((ItemActionBase)__instance).item.SetUseRemainingPercentage((float)data.Value / (float)((ItemActionBase)__instance).item.totalUses);
			}
			if (data.Value == 0 && __instance.consumeOnFullyUsed && (bool)((ItemActionBase)__instance).character && ((ItemActionBase)__instance).character.IsLocal && ((ItemActionBase)__instance).character.data.currentItem == ((ItemActionBase)__instance).item)
			{
				((ItemActionBase)__instance).item.StartCoroutine(((ItemActionBase)__instance).item.ConsumeDelayed());
			}
		}
		return false;
	}
}
