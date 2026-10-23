using System;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

internal static class Game30ExplosionRegression
{
    private static Collider[] colliders;
    private static int lookups;
    internal static int Run(Assembly mod)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var coordinator = mod.GetType("dda.RunCoordinator");
        var active = coordinator.GetField("Active", flags);
        var inRun = coordinator.GetField("InRun", flags);
        object oldActive = active.GetValue(null), oldInRun = inRun.GetValue(null);
        var patches = new Harmony("dda.continued.3.0-explosion-fixture");
        var root = new GameObject("Explosion lookup fixture"); root.SetActive(false);
        int checks = 0;
        try
        {
            colliders = new Collider[] { root.AddComponent<SphereCollider>() };
            var targetType = AccessTools.TypeByName("AOE");
            var target = root.AddComponent(targetType);
            AccessTools.Field(targetType, "range").SetValue(target, 5f);
            patches.Patch(AccessTools.Method(typeof(Physics), "OverlapSphere", new[] { typeof(Vector3), typeof(float), typeof(int) }),
                prefix: new HarmonyMethod(typeof(Game30ExplosionRegression), nameof(Overlap)));
            patches.Patch(AccessTools.Method(AccessTools.TypeByName("CharacterRagdoll"), "TryGetCharacterFromCollider"),
                prefix: new HarmonyMethod(typeof(Game30ExplosionRegression), nameof(Lookup)));
            inRun.SetValue(null, true);
            foreach (int tier in new[] { 0, 9, 20 })
            {
                active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), tier, 0, true, true));
                lookups = 0;
                AccessTools.Method(targetType, "Explode").Invoke(target, null);
                if (lookups != 1) throw new Exception("3.0 explosion must use native collider registry at tier " + tier);
                checks++;
            }
        }
        finally
        {
            patches.UnpatchSelf();
            active.SetValue(null, oldActive); inRun.SetValue(null, oldInRun);
            UnityEngine.Object.DestroyImmediate(root);
            colliders = null;
        }
        return checks;
    }
    private static bool Overlap(ref Collider[] __result) { __result = colliders; return false; }
    // No registered character: verifies the real routing without creating player physics.
    private static bool Lookup(ref bool __result) { lookups++; __result = false; return false; }
}
