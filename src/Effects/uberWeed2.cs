// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;
using UnityEngine;

namespace dda;

[HarmonyPatch(typeof(TumbleWeed), "OnCollisionEnter")]
internal static class uberWeed2
{
	internal static bool Prefix(TumbleWeed __instance, Collision collision)
	{
		if (!(Rules.Enabled(9))) return true;
		Character componentInParent = collision.gameObject.GetComponentInParent<Character>();
		if (!componentInParent)
		{
			return false;
		}
		if (!componentInParent.IsLocal)
		{
			return false;
		}
		if (__instance.ignored.Contains(componentInParent))
		{
			return false;
		}
		__instance.StartCoroutine(__instance.IgnoreTarget(componentInParent));
		float value = __instance.transform.localScale.x / __instance.originalScale;
		if (__instance.originalScale == 0f)
		{
			value = 1f;
		}
		value = Mathf.Clamp01(value);
		float num = Mathf.Clamp01(__instance.rig.linearVelocity.magnitude * value * __instance.powerMultiplier);
		if (__instance.testFullPower)
		{
			num = 1f;
		}
		if (num < 0.2f)
		{
			return false;
		}
		componentInParent.Fall(2f * num, 0f);
		componentInParent.AddForceAtPosition(__instance.rig.linearVelocity.normalized * __instance.collisionForce * num, collision.contacts[0].point, 2f);
		if (Rules.Enabled(9))
		{
			componentInParent.refs.afflictions.AddThorn(collision.contacts[0].point, 0);
		}
		componentInParent.refs.afflictions.AddThorn(collision.contacts[0].point, 0);
		if (num > 0.6f)
		{
			componentInParent.refs.afflictions.AddThorn(collision.contacts[0].point, 0);
		}
		return false;
	}
}
