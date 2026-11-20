using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

internal static class Game30ExplosionRegression
{
    private static Collider[] colliders;
    private static int lookups;
    private static Action duringExplosion;
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
            var overrides = AccessTools.Field(targetType, "addlStatusAmountOverrides");
            var extra = AccessTools.Field(targetType, "addtlStatus");
            extra.SetValue(target, Array.CreateInstance(extra.FieldType.GetElementType(), 2));
            var primary = AccessTools.Field(targetType, "statusAmount");
            var amounts = new List<float> { .35f };
            overrides.SetValue(target, amounts);
            foreach (int tier in new[] { 0, 9, 20 })
            {
                active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), tier, 0, true, true));
                foreach (bool blind in new[] { false, true })
                {
                    float baseline = blind ? 20f : .2f;
                    float multiplier = tier >= 9 ? 2f : 1f;
                    primary.SetValue(target, baseline);
                    AccessTools.Field(targetType, "illegalStatus").SetValue(target, blind ? "BLIND" : "");
                    duringExplosion = () => {
                        var current = (List<float>)overrides.GetValue(target);
                        float main = (float)primary.GetValue(target);
                        Check(main == (blind ? baseline : baseline * multiplier), "primary status or blindness");
                        Check(current[0] == .35f * multiplier, "explicit Cold override");
                        Check((current.Count > 1 ? current[1] : main) == baseline * multiplier, "additional slot without an override");
                    };
                    lookups = 0;
                    AccessTools.Method(targetType, "Explode").Invoke(target, null);
                    Check(lookups == 1, "native collider registry");
                    Check(ReferenceEquals(overrides.GetValue(target), amounts) && amounts.Count == 1 && amounts[0] == .35f, "original shared list restored");
                }
            }
            duringExplosion = () => throw new InvalidOperationException("fixture interruption");
            bool interrupted = false;
            try { AccessTools.Method(targetType, "Explode").Invoke(target, null); }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException { Message: "fixture interruption" }) { interrupted = true; }
            Check(interrupted, "native exception propagates");
            Check(ReferenceEquals(overrides.GetValue(target), amounts) && (float)primary.GetValue(target) == 20f, "restore even on native exception");
        }
        finally
        {
            patches.UnpatchSelf();
            active.SetValue(null, oldActive); inRun.SetValue(null, oldInRun);
            UnityEngine.Object.DestroyImmediate(root);
            colliders = null;
            duringExplosion = null;
        }
        return checks;
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Explosion regression: " + message);
            checks++;
        }
    }
    private static bool Overlap(ref Collider[] __result) { duringExplosion?.Invoke(); __result = colliders; return false; }
    // No registered character: verifies the real routing without creating player physics.
    private static bool Lookup(ref bool __result) { lookups++; __result = false; return false; }
}
