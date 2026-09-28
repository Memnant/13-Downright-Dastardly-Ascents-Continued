// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;
using System;
using UnityEngine;

namespace dda;

[HarmonyPatch(typeof(Bodypart), "ParasolDrag")]
internal static class dastardlyParasol
{
	internal static bool Prefix(Bodypart __instance, float drag, float xzDrag, bool ignoreRagdoll = false)
	{
		if (!Rules.Enabled(12) || !ParasolCallScope.Active || !Rules.Owns(__instance.character)) return true;
		if (!ignoreRagdoll)
		{
			drag = Mathf.Lerp(1f, drag, __instance.character.data.currentRagdollControll);
		}
		if (__instance.rig.isKinematic)
		{
			return false;
		}
		if (__instance.rig.linearVelocity.y < 0f)
		{
			if (Rules.Enabled(12))
			{
				__instance.rig.linearVelocity -= new Vector3(0f, 200f * Time.deltaTime, 0f);
			}
			else
			{
				__instance.rig.linearVelocity = new Vector3(__instance.rig.linearVelocity.x * xzDrag, __instance.rig.linearVelocity.y * drag, __instance.rig.linearVelocity.z * xzDrag);
			}
		}
		return false;
	}
}

// ParasolDrag is also called by Glider. Only the actual parasol call may apply the old penalty.
[HarmonyPatch(typeof(CharacterMovement), "ApplyParasolDrag")]
internal static class ParasolCallScope
{
    [ThreadStatic] private static int depth;
    internal static bool Active => depth > 0;
    private static void Prefix() => depth++;
    private static Exception Finalizer(Exception __exception) { depth--; return __exception; }
}

[HarmonyPatch(typeof(Glider), "FixedUpdate")]
internal static class ContinuedGlider
{
    private static void Prefix(Glider __instance, out float? __state)
    {
        __state = null;
        if (!Rules.Enabled(12)) return;
        __state = __instance.stamUse;
        __instance.stamUse *= 1.5f;
    }
    private static Exception Finalizer(Glider __instance, float? __state, Exception __exception)
    {
        if (__state.HasValue) __instance.stamUse = __state.Value;
        return __exception;
    }
}
