using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Zorro.Core;

namespace dda;

[HarmonyPatch(typeof(FogSphere), "SetSharderVars")]
internal static class ChasingFogTint
{
    internal const float VisibilityDistance = 150f;
    private const float VisibilityFadeWidth = 40f;
    private sealed class State
    {
        internal readonly MaterialPropertyBlock original = new();
        internal bool applied;
    }
    private static ConditionalWeakTable<FogSphere, State> states = new();
    private static readonly List<WeakReference<FogSphere>> touched = new();
    private static readonly int Tint = Shader.PropertyToID("_FogtintColor");
    private static readonly int DeepFog = Shader.PropertyToID("DeepFogColor");
    private static readonly int VisibilityOffset = Shader.PropertyToID("_DistanceVisibilityOffset");
    private static readonly int VisibilitySoftness = Shader.PropertyToID("_DistanceVisibilitysoftness");

    private static void Prefix(FogSphere __instance)
    {
        // Restore the complete unmodified property block, including an absent tint
        // override, before the native code refreshes its current radius/position.
        if (__instance.rend != null && states.TryGetValue(__instance, out var state) && state.applied)
        { __instance.rend.SetPropertyBlock(state.original); state.applied = false; }
    }
    private static void Postfix(FogSphere __instance)
    {
        if (__instance.rend == null || __instance.mpb == null || !MapHandler.Exists) return;
        var map = Singleton<MapHandler>.Instance;
        if (map.segments == null || map.currentSegment < 0 || map.currentSegment >= map.segments.Length || map.segments[map.currentSegment] == null) return;
        Color tint;
        if (Rules.Enabled(14) && map.GetCurrentBiome() == Biome.BiomeType.Volcano)
            tint = new Color(.3f, 0, 0, 0); // Original author's hot fog tint.
        else if (SwampChasingFog.ActiveRegion)
        {
            tint = Shader.GetGlobalColor(DeepFog); // Native Gloom profile's colour.
            if (tint.r <= 0 && tint.g <= 0 && tint.b <= 0) tint = new Color(.45f, .35f, .55f, 0);
            tint.a = 0;
        }
        else return;
        if (!states.TryGetValue(__instance, out var state))
        { state = new State(); states.Add(__instance, state); touched.Add(new WeakReference<FogSphere>(__instance)); }
        __instance.rend.GetPropertyBlock(state.original);
        __instance.mpb.SetColor(Tint, tint);
        // GD/SphereFog fades between offset and offset + softness, measured from
        // the camera to the sphere boundary (with the native texture distortion).
        // Keep the 40 m transition and move its nominal far edge to 150 m.
        __instance.mpb.SetFloat(VisibilityOffset, VisibilityDistance - VisibilityFadeWidth);
        __instance.mpb.SetFloat(VisibilitySoftness, VisibilityFadeWidth);
        __instance.rend.SetPropertyBlock(__instance.mpb);
        state.applied = true;
    }
    internal static void Reset()
    {
        foreach (var reference in touched)
            if (reference.TryGetTarget(out var sphere) && sphere != null && sphere.rend != null && states.TryGetValue(sphere, out var state) && state.applied)
                sphere.rend.SetPropertyBlock(state.original);
        touched.Clear(); states = new ConditionalWeakTable<FogSphere, State>();
    }
}
