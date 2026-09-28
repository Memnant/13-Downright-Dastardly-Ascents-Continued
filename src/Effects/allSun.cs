// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace dda;

[HarmonyPatch(typeof(OrbFogHandler), "Update")]
internal static class allSun
{
	public static float timeTillNext = 5f;

	internal static void Postfix(OrbFogHandler __instance)
	{
		if (!(Rules.Enabled(19) || Rules.Enabled(20))) return;
		if (Character.localCharacter == null || !MapHandler.Exists || DayNightManager.instance == null || DayNightManager.instance.sun == null) return;
		if (!EveryMapSnow.Excluded && timeTillNext < 0f && PhotonNetwork.IsMasterClient && (Rules.Enabled(20)))
		{
			timeTillNext = 5f; // Retry later if no safe ground exists near a living scout.
			foreach (Character allCharacter in Character.AllCharacters)
			{
				if (allCharacter != null && !allCharacter.data.dead && TornadoBoundary.TrySpawn(allCharacter.Center, out var spawnPosition))
				{
					GameObject gameObject = PhotonNetwork.Instantiate("Tornado", spawnPosition, Quaternion.identity, 0, new object[] { customAI.Marker });
					gameObject.GetComponent<PhotonView>().RPC("RPCA_InitTornado", RpcTarget.All, gameObject.GetComponent<PhotonView>().ViewID);
					timeTillNext = Random.Range(40f, 140f);
					break;
				}
			}
		}
		timeTillNext -= Time.deltaTime;
		Transform transform = DayNightManager.instance.sun.transform;
		RaycastHit raycastHit = HelperFunctions.LineCheck(Character.localCharacter.Center + transform.forward * -1000f, Character.localCharacter.Center, HelperFunctions.LayerType.AllPhysical);
		if ((raycastHit.transform == null || raycastHit.transform.root == Character.localCharacter.transform.root) && Application.isPlaying && Character.localCharacter != null && (Rules.Enabled(19)) && Singleton<MapHandler>.Instance.GetCurrentBiome() != Biome.BiomeType.Mesa && DayNightManager.instance.sun.intensity > 1f)
		{
			if (Singleton<MapHandler>.Instance.GetCurrentBiome() == Biome.BiomeType.Alpine && Singleton<MapHandler>.Instance.GetCurrentSegment() == Segment.Alpine)
			{
				Character.localCharacter.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Cold, 0.005f * Time.deltaTime, false, true, true);
			}
			else
			{
				Character.localCharacter.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Hot, 0.005f * Time.deltaTime, false, true, true);
			}
		}
	}
}
