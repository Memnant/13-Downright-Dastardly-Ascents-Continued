using UnityEngine;
using Zorro.Core;

namespace dda;

internal static class Tier20Swamp
{
    internal const float DayRecoveryPerSecond = 0.02f / 3f;
    internal static bool Active
    {
        get
        {
            if (!Rules.Enabled(20) || !MapHandler.Exists) return false;
            var map = Singleton<MapHandler>.Instance;
            // Citadel also uses Swamp in PEAK 2.4.c; only Caldera is the swamp map.
            return map.GetCurrentSegment() == Segment.Caldera && map.GetCurrentBiome() == Biome.BiomeType.Swamp;
        }
    }
    internal static Vector2 SnowRestRange => Active ? new Vector2(60f, 120f) : new Vector2(30f, 90f);
    internal static float NextSnowRest()
    {
        var range = SnowRestRange;
        return Random.Range(range.x, range.y);
    }
}
