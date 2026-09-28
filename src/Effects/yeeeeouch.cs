// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;

namespace dda;

[HarmonyPatch(typeof(Luggage), "Interact_CastFinished")]
public static class yeeeeouch
{
	internal static void Prefix(Luggage __instance, Character interactor)
	{
		if (!(Rules.Enabled(11))) return;
		if (Rules.Enabled(11))
		{
			if (__instance.gameObject.name == "LuggageSmall" || __instance.gameObject.name == "LuggageSmall (2)")
			{
				interactor.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Injury, 0.06f, false, true, true);
			}
			else if (__instance.gameObject.name == "LuggageBig")
			{
				interactor.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Injury, 0.1f, false, true, true);
			}
			else if (__instance.gameObject.name == "LuggageEpic")
			{
				interactor.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Injury, 0.16f, false, true, true);
			}
			else if (__instance.gameObject.name == "scout statue")
			{
				interactor.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Injury, 0.2f, false, true, true);
				interactor.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Curse, 0.06f, false, true, true);
			}
			else
			{
				interactor.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Injury, 0.1f, false, true, true);
			}
		}
	}
}
