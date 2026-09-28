// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;
using Zorro.Core;

namespace dda;

[HarmonyPatch(typeof(LavaRising), "Update")]
internal static class activeKiln
{
	internal static void Prefix(LavaRising __instance)
	{
		if (!Rules.Enabled(15) || !MapHandler.Exists) return;
        var map = Singleton<MapHandler>.Instance;
        if (map.GetCurrentSegment() != __instance.requiredSegment) return;
        bool kiln = __instance.risingFieldType == LavaRising.RisingFieldType.Lava &&
            __instance.requiredSegment == Segment.TheKiln && map.GetCurrentBiome() == Biome.BiomeType.Volcano;
        // Level_3's Temple_Segment is serialized as BiomeType.Swamp (8), not Temple (9).
        // Identify the native Gloom field in the final climbing segment instead.
        bool citadel = __instance.risingFieldType == LavaRising.RisingFieldType.Gloom &&
            __instance.requiredSegment == Segment.TheKiln;
        bool nadir = __instance.risingFieldType == LavaRising.RisingFieldType.VoidGhosts &&
            __instance.requiredSegment == Segment.Void;
		if (kiln || citadel || nadir)
		{
            // Divide the captured map value once; repeated updates must not compound speed.
            float original = EffectState.Original(__instance, "rising.travelTime", __instance.travelTime);
            if (original <= 0f || float.IsNaN(original) || float.IsInfinity(original)) return;
            float travel = kiln ? 700f : original / 2.5f;
            bool changed = __instance.travelTime != travel || (!nadir && __instance.initialWaitTime != 30f);
			if (!nadir && __instance.initialWaitTime != 30f) EffectState.Set(__instance, "initialWaitTime", 30f);
			if (__instance.travelTime != travel) EffectState.Set(__instance, "travelTime", travel);
            if (changed && !kiln) Plugin.Log.LogInfo($"Applied {(nadir ? "Nadir petrifying fog" : "Citadel Gloom")} speed x2.5: rise={travel:F2}s; native event/readiness/rest protections retained.");
		}
	}
}
