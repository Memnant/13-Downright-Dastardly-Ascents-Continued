// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;
using UnityEngine;
using Zorro.Core;

namespace dda;

[HarmonyPatch(typeof(WindChillZone), "ApplyStatus")]
internal static class intenseWinds
{
	internal static bool Prefix(WindChillZone __instance)
	{
		if (WeatherGrace.Suppress(__instance)) return false;
		if (!(Rules.Enabled(14) || Rules.Enabled(20))) return true;
		if (Character.localCharacter == null || !MapHandler.Exists || DayNightManager.instance == null) return true;
		if (__instance.name != "WindStorm" && __instance.name != "SnowStorm" && __instance.name != "RainStorm") return true;
		if (Rules.Enabled(14))
		{
			if (__instance.name == "WindStorm")
			{
				EffectState.Set(__instance, "windForce", 30f);
				if (Singleton<MapHandler>.Instance.GetCurrentBiome() == Biome.BiomeType.Roots && Singleton<MapHandler>.Instance.GetCurrentSegment() == Segment.Tropics)
				{
					EffectState.Set(__instance, "windForce", 50f);
				}
				else if (Singleton<MapHandler>.Instance.GetCurrentSegment() == Segment.TheKiln || Singleton<MapHandler>.Instance.GetCurrentSegment() == Segment.Peak)
				{
					EffectState.Set(__instance, "windForce", 0f);
				}
			}
			__instance.windPlayerFactor = WindChillZone.GetWindIntensityAtPoint(Character.localCharacter.Center, __instance.lightVolumeSampleThreshold_lower, __instance.lightVolumeSampleThreshold_margin);
			if ((Rules.Enabled(20)) && __instance.name == "SnowStorm")
			{
				if (Singleton<MapHandler>.Instance.GetCurrentBiome() != Biome.BiomeType.Alpine || Singleton<MapHandler>.Instance.GetCurrentSegment() != Segment.Alpine)
				{
					EffectState.Set(__instance, "windForce", 12f);
					EffectState.Set(__instance, "statusApplicationPerSecond", 0.01f);
				}
				else
				{
					EffectState.Set(__instance, "windForce", 20f);
					EffectState.Set(__instance, "statusApplicationPerSecond", 0.07f);
				}
			}
			if (__instance.name == "SnowStorm")
			{
				if ((Singleton<MapHandler>.Instance.GetCurrentSegment() == Segment.Alpine && Singleton<MapHandler>.Instance.GetCurrentSegment() != Segment.Caldera) || DayNightManager.instance.isDay < 0.5f)
				{
					Character.localCharacter.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Cold, __instance.windPlayerFactor * __instance.statusApplicationPerSecond * Time.deltaTime * Mathf.Clamp01(__instance.hasBeenActiveFor * 0.2f) * 1.2f, false, true, true);
				}
				else if (Singleton<MapHandler>.Instance.GetCurrentSegment() != Segment.Caldera)
				{
					Character.localCharacter.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Hot, __instance.windPlayerFactor * __instance.statusApplicationPerSecond * Time.deltaTime * Mathf.Clamp01(__instance.hasBeenActiveFor * 0.2f), false, true, true);
				}
			}
			if (__instance.name == "RainStorm")
			{
				if (Character.localCharacter.data.groundedFor > 0.5f || Character.localCharacter.data.isClimbing)
				{
					Character.localCharacter.data.slippy = Mathf.Clamp01(Mathf.Max(Character.localCharacter.data.slippy, __instance.windPlayerFactor * 200f));
				}
				else
				{
					RaycastHit raycastHit = HelperFunctions.LineCheck(Character.localCharacter.Center + new Vector3(0f, 500f, 0f), Character.localCharacter.Center, HelperFunctions.LayerType.AllPhysical);
					if (raycastHit.transform == null || raycastHit.transform.root == Character.localCharacter.transform.root)
					{
						if (Singleton<MapHandler>.Instance.GetCurrentSegment() != Segment.Tropics)
						{
							Character.localCharacter.GetBodypart(BodypartType.Hip).rig.linearVelocity -= new Vector3(0f, 200f * Time.deltaTime, 0f);
						}
						else
						{
							Character.localCharacter.GetBodypart(BodypartType.Hip).rig.linearVelocity -= new Vector3(0f, 400f * Time.deltaTime, 0f);
						}
					}
				}
			}
		}
		else
		{
			__instance.windPlayerFactor = WindChillZone.GetWindIntensityAtPoint(Character.localCharacter.Center, __instance.lightVolumeSampleThreshold_lower, __instance.lightVolumeSampleThreshold_margin);
			Character.localCharacter.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Cold, __instance.windPlayerFactor * __instance.statusApplicationPerSecond * Time.deltaTime * Mathf.Clamp01(__instance.hasBeenActiveFor * 0.2f), false, true, true);
			if (__instance.setSlippy)
			{
				Character.localCharacter.data.slippy = Mathf.Clamp01(Mathf.Max(Character.localCharacter.data.slippy, __instance.windPlayerFactor * 10f));
			}
		}
		return false;
	}
}
