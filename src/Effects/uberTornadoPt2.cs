// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace dda;

[HarmonyPatch(typeof(Tornado), "CapturedCharacter")]
internal static class uberTornadoPt2
{
	internal static bool Prefix(Tornado __instance)
	{
		if (!(Rules.Enabled(14))) return true;
		foreach (Character caughtCharacter in new System.Collections.Generic.List<Character>(__instance.caughtCharacters))
		{
			if (caughtCharacter == null || __instance.ignoredCharacters.Contains(caughtCharacter))
			{
				continue;
			}
			float num = 15f;
			Vector3 vector = (caughtCharacter.Center - __instance.transform.position).Flat().normalized * num;
			Vector3 vector2 = __instance.transform.position + vector;
			Vector3 vector3 = (vector2 - caughtCharacter.Center).Flat();
			Vector3 normalized = Vector3.Cross(Vector3.up, vector).normalized;
			if (Rules.Enabled(14))
			{
				caughtCharacter.AddForce(normalized * __instance.force * 2f, 2f, 2f);
				caughtCharacter.AddForce(vector3 * __instance.force * 0.2f * 2f, 2f, 2f);
				caughtCharacter.AddForce(Vector3.up * (19f + Mathf.Abs(__instance.Height(caughtCharacter))) * 2f, 2f, 2f);
			}
			else
			{
				caughtCharacter.AddForce(normalized * __instance.force, 1f, 1f);
				caughtCharacter.AddForce(vector3 * __instance.force * 0.2f, 1f, 1f);
				caughtCharacter.AddForce(Vector3.up * (19f + Mathf.Abs(__instance.Height(caughtCharacter) * 1f)), 1f, 1f);
			}
			caughtCharacter.ClampSinceGrounded(0.5f);
			if (caughtCharacter.IsLocal)
			{
				if (Rules.Enabled(14))
				{
					caughtCharacter.GetBodypartRig(BodypartType.Torso).AddTorque(Vector3.up * 200f, ForceMode.Acceleration);
					caughtCharacter.GetBodypartRig(BodypartType.Hip).AddTorque(Vector3.up * 200f, ForceMode.Acceleration);
					caughtCharacter.GetBodypartRig(BodypartType.Torso).AddTorque(vector.normalized * 100f, ForceMode.Acceleration);
					caughtCharacter.GetBodypartRig(BodypartType.Hip).AddTorque(vector.normalized * 100f, ForceMode.Acceleration);
					if (MapHandler.Exists && Singleton<MapHandler>.Instance.GetCurrentBiome() == Biome.BiomeType.Mesa)
					{
						caughtCharacter.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Drowsy, 0.05f * Time.deltaTime, false, true, true);
					}
				}
				else
				{
					caughtCharacter.GetBodypartRig(BodypartType.Torso).AddTorque(Vector3.up * 200f, ForceMode.Acceleration);
					caughtCharacter.GetBodypartRig(BodypartType.Hip).AddTorque(Vector3.up * 200f, ForceMode.Acceleration);
					caughtCharacter.GetBodypartRig(BodypartType.Torso).AddTorque(vector.normalized * 100f, ForceMode.Acceleration);
					caughtCharacter.GetBodypartRig(BodypartType.Hip).AddTorque(vector.normalized * 100f, ForceMode.Acceleration);
				}
			}
			else
			{
				caughtCharacter.GetBodypartRig(BodypartType.Torso).AddTorque(Vector3.up * 500f, ForceMode.Acceleration);
				caughtCharacter.GetBodypartRig(BodypartType.Hip).AddTorque(Vector3.up * 500f, ForceMode.Acceleration);
				caughtCharacter.GetBodypartRig(BodypartType.Torso).AddTorque(vector.normalized * 500f, ForceMode.Acceleration);
				caughtCharacter.GetBodypartRig(BodypartType.Hip).AddTorque(vector.normalized * 500f, ForceMode.Acceleration);
			}
			caughtCharacter.refs.movement.ApplyExtraDrag(0.95f, ignoreRagdoll: true);
			caughtCharacter.RPCA_Fall(0.5f, 0f);
			if (caughtCharacter.IsLocal && __instance.LetTargetGo(caughtCharacter, vector2))
			{
				__instance.view.RPC("RPCA_ThrowPlayer", RpcTarget.All, caughtCharacter.refs.view.ViewID);
			}
		}
		return false;
	}
}
