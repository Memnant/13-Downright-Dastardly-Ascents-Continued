// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace dda;

[HarmonyPatch(typeof(BeeSwarm), "Update")]
internal static class bees
{
	internal static bool Prefix(BeeSwarm __instance)
	{
		if (!(Rules.Enabled(9))) return true;
		if (__instance.dispersing)
		{
			return false;
		}
		if (!PhotonNetwork.InRoom)
		{
			return false;
		}
		if (__instance.photonView.IsMine)
		{
			bool flag = __instance.beehiveDangerTick > 0f;
			if (__instance.beesAngry != flag)
			{
				__instance.photonView.RPC("SetBeesAngryRPC", RpcTarget.AllBuffered, flag);
			}
		}
		if (Rules.Enabled(9))
		{
			__instance.stingerField.statusAmountPerSecond = (__instance.beesAngry ? __instance.poisonOverTimeAngry : __instance.poisonOverTime) * 20f;
		}
		else
		{
			__instance.stingerField.statusAmountPerSecond = (__instance.beesAngry ? __instance.poisonOverTimeAngry : __instance.poisonOverTime);
		}
		if (__instance.beehive == null)
		{
			__instance.TryGetBeehive();
		}
		__instance.UpdateAggro();
		if (!__instance.photonView.IsMine)
		{
			return false;
		}
		if (__instance.beesAngry)
		{
			__instance.beehiveDangerTick = Mathf.Max(__instance.beehiveDangerTick - Time.deltaTime, 0f);
		}
		return false;
	}
}
