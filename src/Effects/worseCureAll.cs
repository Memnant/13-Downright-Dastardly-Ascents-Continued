// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using System;
using HarmonyLib;
using UnityEngine;

namespace dda;

[HarmonyPatch(typeof(Action_ClearAllStatus), "RunAction")]
internal static class worseCureAll
{
	internal static bool Prefix(Action_ClearAllStatus __instance)
	{
		if (!(Rules.Enabled(12))) return true;
		int num = Enum.GetNames(typeof(CharacterAfflictions.STATUSTYPE)).Length;
		for (int i = 0; i < num; i++)
		{
			CharacterAfflictions.STATUSTYPE sTATUSTYPE = (CharacterAfflictions.STATUSTYPE)i;
			if (!__instance.defaultExclusions.Contains(sTATUSTYPE) && (!__instance.excludeCurse || sTATUSTYPE != CharacterAfflictions.STATUSTYPE.Curse) && !__instance.otherExclusions.Contains(sTATUSTYPE))
			{
				if (Rules.Enabled(12))
				{
					((ItemActionBase)__instance).character.refs.afflictions.SubtractStatus(sTATUSTYPE, i <= 11 ? 0.5f : 5f);
				}
				else
				{
					((ItemActionBase)__instance).character.refs.afflictions.SubtractStatus(sTATUSTYPE, Mathf.Abs(5));
				}
			}
		}
		return false;
	}
}
