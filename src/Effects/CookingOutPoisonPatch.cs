// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;
using UnityEngine;

namespace dda;

[HarmonyPatch(typeof(CookingBehavior_DisableScripts), "TriggerBehaviour")]
internal static class CookingOutPoisonPatch
{
	internal static void Postfix(CookingBehavior_DisableScripts __instance)
	{
		if (!(Rules.Enabled(12))) return;
		if (!Rules.Enabled(12))
		{
			return;
		}
		MonoBehaviour[] scriptsToDisable = __instance.scriptsToDisable;
		foreach (MonoBehaviour monoBehaviour in scriptsToDisable)
		{
			if (monoBehaviour != null)
			{
				monoBehaviour.enabled = true;
			}
		}
	}
}
