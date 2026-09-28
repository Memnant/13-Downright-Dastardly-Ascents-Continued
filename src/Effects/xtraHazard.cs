// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace dda;

[HarmonyPatch(typeof(SlipperyJellyfish), "Trigger")]
internal static class xtraHazard
{
	internal static bool Prefix(SlipperyJellyfish __instance, int targetID)
	{
		if (!(Rules.Enabled(9))) return true;
		var view = PhotonView.Find(targetID);
		Character component = view == null ? null : view.GetComponent<Character>();
		if (component == null)
		{
			return false;
		}
		float num = 1f;
		float num2 = 1f;
		Rigidbody bodypartRig = component.GetBodypartRig(BodypartType.Foot_R);
		Rigidbody bodypartRig2 = component.GetBodypartRig(BodypartType.Foot_L);
		Rigidbody bodypartRig3 = component.GetBodypartRig(BodypartType.Hip);
		Rigidbody bodypartRig4 = component.GetBodypartRig(BodypartType.Head);
		if (Rules.Enabled(9))
		{
			num = 5f;
			num2 = 3f;
		}
		else
		{
			num = 1f;
			num2 = 1f;
		}
		component.RPCA_Fall(2f * num, 0f);
		bodypartRig.AddForce((component.data.lookDirection_Flat + Vector3.up) * 200f, ForceMode.Impulse);
		bodypartRig2.AddForce((component.data.lookDirection_Flat + Vector3.up) * 200f, ForceMode.Impulse);
		bodypartRig3.AddForce(Vector3.up * 1500f * num2, ForceMode.Impulse);
		bodypartRig4.AddForce(component.data.lookDirection_Flat * -300f, ForceMode.Impulse);
		CoastalPoison.ApplyJellyfish(component);
		for (int i = 0; i < __instance.slipSFX.Length; i++)
		{
			__instance.slipSFX[i].Play(__instance.transform.position);
		}
		return false;
	}
}
