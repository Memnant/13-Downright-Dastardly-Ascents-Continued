using System.Runtime.CompilerServices;
using Peak;
using UnityEngine;
using Zorro.Core;

namespace dda;

internal static class SwampChasingFog
{
    private sealed class Context
    {
        internal StatusFieldGloom field;
        internal Vector3 center;
        internal float radius, sleepRate;
        internal float nextLookup;
    }
    private static ConditionalWeakTable<MapHandler, Context> contexts = new();
    private static ConditionalWeakTable<OrbFogHandler, object> initialized = new();

    internal static bool ActiveRegion
    {
        get
        {
            if (!Rules.Enabled(18) || !MapHandler.Exists) return false;
            var map = Singleton<MapHandler>.Instance;
            return map.GetCurrentSegment() == Segment.Caldera && map.segments != null &&
                map.currentSegment >= 0 && map.currentSegment < map.segments.Length &&
                map.segments[map.currentSegment] != null && map.GetCurrentBiome() == Biome.BiomeType.Swamp;
        }
    }

    // Resolve the current map's own field and exit campfire once. No per-frame scene search.
    private static Context GetContext()
    {
        if (!ActiveRegion) return null;
        var map = Singleton<MapHandler>.Instance;
        if (!contexts.TryGetValue(map, out var cached))
        { cached = new Context { nextLookup = -1 }; contexts.Add(map, cached); }
        if (cached.field != null) return cached;
        if (Time.unscaledTime < cached.nextLookup) return null;
        cached.nextLookup = Time.unscaledTime + 1f;
        var segment = map.segments[(int)Segment.Caldera];
        var parent = segment.segmentParent;
        var campfire = segment.segmentCampfire;
        if (parent == null || campfire == null || parent.transform.parent == null) return null;
        StatusFieldGloom ambient = null;
        foreach (var candidate in parent.transform.parent.GetComponentsInChildren<StatusFieldGloom>(true))
        {
            // The Citadel field shares the Gloom root but is moved by LavaRising.
            if (candidate.statusType != CharacterAfflictions.STATUSTYPE.Drowsy ||
                candidate.statusAmountPerSecond <= 0 || candidate.GetComponentInParent<LavaRising>(true) != null) continue;
            if (ambient != null) return null; // Unknown layout: never guess which hazard is ambient.
            ambient = candidate;
        }
        if (ambient == null) return null;
        var center = campfire.transform.position;
        var bounds = new Bounds(ambient.transform.position - Vector3.up * ambient.size.y / 2f, ambient.size);
        // A shrinking sphere ends at the exit campfire. Start behind every corner of
        // the native swamp field and the segment entrance, with a 30 m margin.
        float radius = Vector3.Distance(center, parent.transform.position);
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
            radius = Mathf.Max(radius, Vector3.Distance(center,
                bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z))));
        radius += 30f;
        if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 30f || radius > 5000f) return null;
        cached.field = ambient; cached.center = center; cached.radius = radius; cached.sleepRate = ambient.statusAmountPerSecond;
        return cached;
    }

    internal static bool Available(OrbFogHandler handler) => handler.currentID == (int)Segment.Caldera && GetContext() != null;

    internal static void ForgetOrigin(OrbFogHandler handler) => initialized.Remove(handler);
    internal static bool Initialize(OrbFogHandler handler, bool preserveSize = false)
    {
        var context = GetContext();
        if (context == null) return false;
        handler.sphere.fogPoint = context.center;
        if (!preserveSize) handler.currentSize = context.radius;
        initialized.Remove(handler); initialized.Add(handler, new object());
        // Leave player-progress start thresholds at the native disabled values;
        // the tier-18 20-second timer (paused at campfire) starts this new extension.
        return true;
    }

    internal static void EnsureGeometry(OrbFogHandler handler)
    {
        if (handler == null || handler.sphere == null || handler.currentID != (int)Segment.Caldera ||
            handler.origins == null || handler.currentID >= handler.origins.Length || handler.origins[handler.currentID] == null ||
            initialized.TryGetValue(handler, out _) || !ActiveRegion) return;
        // Native continuation/late joining may initialize fog before MapHandler.
        // Install the center when the map is ready; preserve an already received radius.
        Initialize(handler, !Mathf.Approximately(handler.currentSize, handler.origins[handler.currentID].size));
    }

    internal static bool AddStatus(CharacterAfflictions recipient, bool fromRPC, bool playEffects, bool notify, bool ignoreInvincibility, bool ignoreSkeleton)
    {
        var context = GetContext();
        if (context == null || Singleton<OrbFogHandler>.Instance == null || Singleton<OrbFogHandler>.Instance.currentID != (int)Segment.Caldera ||
            !Ascents.fogEnabled || !context.field.isActiveAndEnabled || StatusReplication.Active ||
            !Rules.Owns(recipient.character) || RunSettings.GetValue(RunSettings.SETTINGTYPE.Hazard_SleepyGloom) == 0) return false;
        var point = recipient.character.Head;
        if (GloomSafeZone.TryGetClosestSafeZone(point, out var safe, out _, true) && safe.PointInProtectionRadius(point)) return false;
        if (context.field.doNotApplyIfStatusesMaxed && recipient.statusSum >= 1f) return false;
        // Add TWO native rates. The existing ambient field adds its own ONE rate,
        // when in that field. Tier 17 still applies once; tier 20 swamp is exempt.
        return recipient.AddStatus(CharacterAfflictions.STATUSTYPE.Drowsy, context.sleepRate * 2f * Time.deltaTime,
            fromRPC, playEffects, notify, ignoreInvincibility, ignoreSkeleton);
    }

    internal static void Reset()
    { contexts = new ConditionalWeakTable<MapHandler, Context>(); initialized = new ConditionalWeakTable<OrbFogHandler, object>(); }
}
