// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;
using Peak.Afflictions;
using Photon.Pun;
using UnityEngine;

namespace dda;

[HarmonyPatch(typeof(Bugfix), "LateUpdate")]
internal static class uberTick
{
	internal static bool Prefix(Bugfix __instance)
	{
		if (!(Rules.Enabled(9))) return true;
		__instance.counter += Time.deltaTime;
		__instance.lifeTime += Time.deltaTime;
		if ((bool)__instance.targetCharacter && !__instance.targetCharacter.data.dead)
		{
			if (__instance.targetCharacter.IsLocal && __instance.counter > 0.5f)
			{
				__instance.targetCharacter.refs.afflictions.AddAffliction(new Affliction_PreventPoisonHealing(30f));
				if (__instance.totalStatusApplied < __instance.maxStatus || __instance.targetCharacter.refs.afflictions.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Poison) < 0.5f)
				{
					__instance.targetCharacter.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Poison, 0.05f, false, true, true);
					__instance.totalStatusApplied += 0.05f;
				}
				__instance.counter = 0f;
			}
			Vector3 position = __instance.leg.TransformPoint(__instance.localPos);
			__instance.transform.position = position;
			Quaternion rotation = Quaternion.LookRotation(__instance.leg.TransformDirection(__instance.forward), __instance.leg.TransformDirection(__instance.up));
			__instance.transform.rotation = rotation;
			__instance.transform.localScale = Vector3.LerpUnclamped(Vector3.zero, Vector3.one, __instance.lifeTime / 300f);
			return false;
		}
		if (__instance.photonView.IsMine)
		{
			PhotonNetwork.Destroy(__instance.gameObject);
		}
		return false;
	}
}
