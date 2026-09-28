// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;
using UnityEngine;

namespace dda;

[HarmonyPatch(typeof(StickyCactus), "OnCollide")]
internal static class uberCactus
{
	internal static bool Prefix(StickyCactus __instance, Character character, CollisionModifier modifier, Collision collision, Bodypart bodypart)
	{
		if (!(Rules.Enabled(9))) return true;
		if (!character.IsLocal)
		{
			return false;
		}
		if (character.data.isInvincible)
		{
			return false;
		}
		if (bodypart.partType == BodypartType.Head && !Rules.Enabled(9))
		{
			return false;
		}
		if (bodypart.partType == BodypartType.Torso && !Rules.Enabled(9))
		{
			return false;
		}
		if (bodypart.partType == BodypartType.Hip && !Rules.Enabled(9))
		{
			return false;
		}
		if (character.TryStickBodypart(bodypart, collision.contacts[0].point, CharacterAfflictions.STATUSTYPE.Thorns, 0f) && __instance.applyThorn)
		{
			character.refs.afflictions.AddThorn(collision.contacts[0].point, 0);
			if (Rules.Enabled(9))
			{
				character.refs.afflictions.AddThorn(collision.contacts[0].point, 0);
			}
		}
		return false;
	}
}
