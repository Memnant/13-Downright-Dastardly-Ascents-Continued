// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;

namespace dda;

[HarmonyPatch(typeof(TornadoSpawner), "Update")]
internal static class uberTornado
{
	internal static void Prefix(TornadoSpawner __instance)
	{
		if (!(Rules.Enabled(14))) return;
		if (Rules.Enabled(14))
		{
			EffectState.Set(__instance, "minSpawnTime", 20f);
			EffectState.Set(__instance, "maxSpawnTime", 120f);
			if (__instance.untilNext > 120f)
			{
				__instance.untilNext = 0f;
			}
		}
	}
}
