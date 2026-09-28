// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;

namespace dda;

[HarmonyPatch(typeof(TiedBalloon), "FixedUpdate")]
internal static class worseBalloon
{
	internal static void Prefix(TiedBalloon __instance)
	{
		if (!(Rules.Enabled(12))) return;
		if (Rules.Enabled(12))
		{
			EffectState.Set(__instance, "floatForce", 5f);
		}
	}
}
