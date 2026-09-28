// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;
using Peak.Afflictions;
using UnityEngine;

namespace dda;

[HarmonyPatch(typeof(Affliction_AdjustDrowsyOverTime), "UpdateEffect")]
internal static class worseLollipop
{
	internal static bool Prefix(Affliction_AdjustDrowsyOverTime __instance)
	{
		if (!(Rules.Enabled(12))) return true;
		if (Rules.Enabled(12))
		{
			__instance.character.refs.afflictions.AdjustStatus(CharacterAfflictions.STATUSTYPE.Drowsy, __instance.statusPerSecond * Time.deltaTime * 3f);
		}
		else
		{
			__instance.character.refs.afflictions.AdjustStatus(CharacterAfflictions.STATUSTYPE.Drowsy, __instance.statusPerSecond * Time.deltaTime);
		}
		return false;
	}
}
