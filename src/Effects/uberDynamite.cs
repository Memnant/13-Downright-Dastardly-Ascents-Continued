// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using System;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace dda;

[HarmonyPatch(typeof(Dynamite), "Update")]
internal static class uberDynamite
{
	internal static bool Prefix(Dynamite __instance)
	{
		if (!(Rules.Enabled(9))) return true;
		__instance.TestLightWick();
		bool value = __instance.GetData<BoolItemData>(DataEntryKey.FlareActive).Value;
		if (value && !__instance.trackable.hasTracker)
		{
			__instance.EnableFlareVisuals();
		}
		__instance.fuseTime = __instance.GetData(DataEntryKey.Fuel, (Func<FloatItemData>)__instance.SetupDefaultFuel).Value;
		__instance.item.SetUseRemainingPercentage(__instance.fuseTime / __instance.startingFuseTime);
		__instance.sparks.gameObject.SetActive(value);
		if (value && ((ItemComponent)__instance).photonView.IsMine)
		{
			__instance.fuseTime -= Time.deltaTime;
			if (__instance.fuseTime <= 0f)
			{
				Debug.Log("BOOOOOOOM!!!");
				if (Character.localCharacter.data.currentItem == __instance.item)
				{
					if (Rules.Enabled(9))
					{
						Character.localCharacter.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Injury, 0.25f, false, true, true);
					}
					Character.localCharacter.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Injury, 0.25f, false, true, true);
					Player.localPlayer.EmptySlot(Character.localCharacter.refs.items.currentSelectedSlot);
					Character.localCharacter.refs.afflictions.UpdateWeight();
				}
				((ItemComponent)__instance).photonView.RPC("RPC_Explode", RpcTarget.All);
				PhotonNetwork.Destroy(__instance.gameObject);
				__instance.item.ClearDataFromBackpack();
				__instance.fuseTime = 0f;
			}
			__instance.GetData(DataEntryKey.Fuel, (Func<FloatItemData>)__instance.SetupDefaultFuel).Value = __instance.fuseTime;
		}
		return false;
	}
}
