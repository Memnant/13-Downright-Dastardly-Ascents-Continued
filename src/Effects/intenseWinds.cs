// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using System;
using HarmonyLib;
using UnityEngine;
using Zorro.Core;

namespace dda;

[HarmonyPatch(typeof(WindChillZone), "ApplyStatus")]
internal static class intenseWinds
{
	internal struct Suppressed { internal bool changed, slippy; internal float status; }

	// Native ApplyStatus still runs: it computes windPlayerFactor and PEAK 3.0's wind
	// climbing-stamina multiplier. Only its single status/slip delivery is switched off
	// for this call; the tiered statuses below replace it, as in the original mod.
	internal static bool Prefix(WindChillZone __instance, Character character, out Suppressed __state)
	{
		__state = default;
		if (WeatherGrace.Suppress(__instance)) return false;
		if (!Rules.Enabled(14) || !Rules.Owns(character) || !MapHandler.Exists || DayNightManager.instance == null) return true;
		if (__instance.name != "WindStorm" && __instance.name != "SnowStorm" && __instance.name != "RainStorm") return true;
		var map = Singleton<MapHandler>.Instance;
		if (__instance.name == "WindStorm")
		{
			float force = map.GetCurrentBiome() == Biome.BiomeType.Roots && map.GetCurrentSegment() == Segment.Tropics ? 50f :
				map.GetCurrentSegment() == Segment.TheKiln || map.GetCurrentSegment() == Segment.Peak ? 0f : 30f;
			EffectState.Set(__instance, nameof(__instance.windForce), force);
		}
		if (Rules.Enabled(20) && __instance.name == "SnowStorm")
		{
			bool alpine = map.GetCurrentBiome() == Biome.BiomeType.Alpine && map.GetCurrentSegment() == Segment.Alpine;
			EffectState.Set(__instance, nameof(__instance.windForce), alpine ? 20f : 12f);
			EffectState.Set(__instance, nameof(__instance.statusApplicationPerSecond), alpine ? 0.07f : 0.01f);
		}
		__state = new Suppressed { changed = true, status = __instance.statusApplicationPerSecond, slippy = __instance.setSlippy };
		__instance.statusApplicationPerSecond = 0f;
		__instance.setSlippy = false;
		return true;
	}

	internal static Exception Finalizer(WindChillZone __instance, Character character, Suppressed __state, Exception __exception)
	{
		if (!__state.changed) return __exception;
		__instance.statusApplicationPerSecond = __state.status;
		__instance.setSlippy = __state.slippy;
		if (__exception == null) ApplyTieredStatus(__instance, character);
		return __exception;
	}

	private static void ApplyTieredStatus(WindChillZone zone, Character character)
	{
		var segment = Singleton<MapHandler>.Instance.GetCurrentSegment();
		var afflictions = character.refs.afflictions;
		float ramp = zone.windPlayerFactor * zone.statusApplicationPerSecond * Time.deltaTime * Mathf.Clamp01(zone.hasBeenActiveFor * 0.2f);
		if (zone.name == "SnowStorm")
		{
			if (segment == Segment.Alpine || DayNightManager.instance.isDay < 0.5f)
				afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Cold, ramp * 1.2f, false, true, true);
			else if (segment != Segment.Caldera)
				afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Hot, ramp, false, true, true);
		}
		if (zone.name != "RainStorm") return;
		if (character.data.groundedFor > 0.5f || character.data.isClimbing)
		{
			character.data.slippy = Mathf.Clamp01(Mathf.Max(character.data.slippy, zone.windPlayerFactor * 200f));
			return;
		}
		RaycastHit hit = HelperFunctions.LineCheck(character.Center + new Vector3(0f, 500f, 0f), character.Center, HelperFunctions.LayerType.AllPhysical);
		if (hit.transform == null || hit.transform.root == character.transform.root)
			character.GetBodypart(BodypartType.Hip).rig.linearVelocity -= new Vector3(0f, (segment == Segment.Tropics ? 400f : 200f) * Time.deltaTime, 0f);
	}
}
