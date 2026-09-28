// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;

namespace dda;

[HarmonyPatch(typeof(Scoutmaster), "IThrow")]
internal static class cursedThrow
{
	internal static void Prefix(Scoutmaster __instance)
	{
		if (!Rules.Enabled(10) || !Rules.Owns(__instance.currentTarget)) return;
		if (Rules.Enabled(10))
		{
			__instance.currentTarget.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Curse, 0.04f, true, true, true);
		}
	}
}
