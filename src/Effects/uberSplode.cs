// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace dda;

[HarmonyPatch(typeof(AOE), "Explode")]
internal static class uberSplode
{
	[HarmonyPatch(typeof(BananaPeel), "RPCA_TriggerBanana")]
	internal static class uberNanas
	{
		internal static bool Prefix(int viewID, BananaPeel __instance)
		{
				if (!(Rules.Enabled(9))) return true;
			var view = PhotonView.Find(viewID);
			Character component = view == null ? null : view.GetComponent<Character>();
			if (component == null)
			{
				return false;
			}
			float num = 1f;
			float num2 = 1f;
			if (Rules.Enabled(9))
			{
				num = 5f;
				num2 = 3f;
			}
			else
			{
				num = 1f;
				num2 = 1f;
			}
			__instance.GetComponent<Rigidbody>().AddForce((component.data.lookDirection_Flat * 0.5f + Vector3.up) * 40f, ForceMode.Impulse);
			Rigidbody bodypartRig = component.GetBodypartRig(BodypartType.Foot_R);
			Rigidbody bodypartRig2 = component.GetBodypartRig(BodypartType.Foot_L);
			Rigidbody bodypartRig3 = component.GetBodypartRig(BodypartType.Hip);
			Rigidbody bodypartRig4 = component.GetBodypartRig(BodypartType.Head);
			component.RPCA_Fall(2f * num, 0f);
			bodypartRig.AddForce((component.data.lookDirection_Flat + Vector3.up) * 200f, ForceMode.Impulse);
			bodypartRig2.AddForce((component.data.lookDirection_Flat + Vector3.up) * 200f, ForceMode.Impulse);
			bodypartRig3.AddForce(Vector3.up * 1500f * num2, ForceMode.Impulse);
			bodypartRig4.AddForce(component.data.lookDirection_Flat * -300f, ForceMode.Impulse);
			for (int i = 0; i < __instance.slipSFX.Length; i++)
			{
				__instance.slipSFX[i].Play(__instance.transform.position);
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(BreakableBridge), "FixedUpdate")]
	internal static class dontGoBeyondTheRicketyBridge
	{
		internal static bool Prefix(BreakableBridge __instance)
		{
				if (!(Rules.Enabled(9))) return true;
			__instance.peopleOnBridge = 0;
			if (__instance.debug)
			{
				Debug.Log($"FixedUpdate: {Time.frameCount}, peopleOnBridge: {__instance.peopleOnBridge}");
			}
			__instance.peopleOnBridge = 0;
			foreach (Character item in __instance.peopleOnBridgeDict.Keys.ToList())
			{
				__instance.peopleOnBridgeDict[item] += Time.deltaTime;
				if (__instance.peopleOnBridgeDict[item] < 0.25f)
				{
					if (Rules.Enabled(9))
					{
						__instance.peopleOnBridge += 200;
					}
					else
					{
						__instance.peopleOnBridge++;
					}
				}
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(Lava), "HitPlayer")]
	internal static class uberlave
	{
		internal static bool Prefix(Lava __instance, Character item)
		{
				if (!(Rules.Enabled(9))) return true;
			if (Rules.Enabled(9))
			{
				item.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Injury, 1f, false, true, true);
				item.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Hot, 1f, false, true, true);
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(EruptionSpawner), "Update")]
	internal static class lottaEruptions
	{
		internal static bool Prefix(EruptionSpawner __instance)
		{
				if (!(Rules.Enabled(9))) return true;
			if (!PhotonNetwork.IsMasterClient)
			{
				return false;
			}
			if (!HelperFunctions.AnyPlayerInZRange(__instance.min.position.z, __instance.max.position.z))
			{
				return false;
			}
			__instance.counter -= Time.deltaTime;
			if (__instance.counter < 0f)
			{
				if (Rules.Enabled(9))
				{
					__instance.counter = Random.Range(-5f, 12f);
				}
				else
				{
					__instance.counter = Random.Range(-5f, 15f);
				}
				Vector3 position = __instance.transform.position;
				position.x += Random.Range(-155f, 155f);
				position.z += Random.Range(-140f, 140f);
				__instance.photonView.RPC("RPCA_SpawnEruption", RpcTarget.All, position);
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(ClimbModifierSurface), "OnClimb")]
	internal static class uberRocks
	{
		internal static void Prefix(ClimbModifierSurface __instance, out float? __state)
		{
			__state = null;
			// New petrifying surfaces use integer AddPetrify and the native hazard setting.
			// Run the full current method; only legacy status surfaces get the old multiplier.
			if (!Rules.Enabled(9) || !__instance.applyStatus || __instance.applyPetrify) return;
			__state = __instance.statusAmount;
			__instance.statusAmount *= 2f;
		}
		internal static System.Exception Finalizer(ClimbModifierSurface __instance, float? __state, System.Exception __exception)
		{
			if (__state.HasValue) __instance.statusAmount = __state.Value;
			return __exception;
		}
	}

	[HarmonyPatch(typeof(CollisionModifier), "Collide")]
	internal static class urchinsandvinesiguess
	{
		internal static bool Prefix(CollisionModifier __instance, Character character, out CoastalPoison.ContactState __state)
		{
			__state = default;
			// A petrifying stone uses damage as whole points. Its legacy statusType is
			// still Cold in PEAK 2.4.c; rewriting 2 to .02 rounds petrification to zero.
			if (!Rules.Enabled(9) || __instance.applyPetrify) return true;
            if (__instance.statusType == CharacterAfflictions.STATUSTYPE.Poison && CoastalPoison.IsUrchin(__instance))
            {
                __state = new CoastalPoison.ContactState(__instance, character);
                return true;
            }
			if (Rules.Enabled(9))
			{
				if (__instance.statusType == CharacterAfflictions.STATUSTYPE.Cold)
				{
					EffectState.Set(__instance, nameof(__instance.damage), 0.02f);
				}
				else if (__instance.statusType == CharacterAfflictions.STATUSTYPE.Hot)
				{
					EffectState.Set(__instance, nameof(__instance.damage), 0.02f);
				}
				else if (__instance.statusType == CharacterAfflictions.STATUSTYPE.Poison)
				{
					EffectState.Set(__instance, nameof(__instance.damage), 0.5f);
				}
			}
			if (__instance.name == "HangBridge")
			{
				EffectState.Set(__instance, nameof(__instance.damage), 0f);
			}
			return true;
		}

        internal static System.Exception Finalizer(CollisionModifier __instance, CoastalPoison.ContactState __state, System.Exception __exception)
        {
            __state.Restore(__instance);
            return __exception;
        }
	}

	[HarmonyPatch(typeof(StatusEmitter), "Update")]
	internal static class uberEmitter
	{
		internal static void Prefix(StatusEmitter __instance, out float? __state)
		{
			__state = null;
			// Native Update uses a local-player distance check, wind/overlap protection and
			// warning timing. Avoid the legacy global physics query and per-tick LINQ sets.
			if (!Rules.Enabled(9) || __instance.amount <= 0f) return;
			__state = __instance.amount;
			__instance.amount *= 4f;
		}
		internal static System.Exception Finalizer(StatusEmitter __instance, float? __state, System.Exception __exception)
		{
			if (__state.HasValue) __instance.amount = __state.Value;
			return __exception;
		}
	}

	// Scale the explosion's own parameters and let the native 3.0 Explode run, so line of sight,
	// blindness, fall-damage caps, afflictions, carried scouts and grounded-item rules stay intact.
	internal const float StatusScale = 2f;
	internal const float FallScale = 3f;
	// The old body used AddForce(..., 2.1f, 5.2f) instead of the native 0.7f-1.3f random range:
	// on average 3.65 times the native push. Items were not scaled, so their multiplier is divided back.
	internal const float KnockbackScale = 3.65f;
	[System.ThreadStatic] private static int explosionDepth;
	internal static bool InExplosion => explosionDepth > 0;
	internal struct Scaled
	{
		internal bool changed, status;
		internal float statusAmount, fallTime, knockback, vertical, items;
	}

	internal static void Prefix(AOE __instance, out Scaled __state)
	{
		__state = default;
		if (!Rules.Enabled(9)) return;
		__state = new Scaled {
			changed = true, status = string.IsNullOrEmpty(__instance.illegalStatus), statusAmount = __instance.statusAmount,
			fallTime = __instance.fallTime, knockback = __instance.knockback,
			vertical = __instance.additionalVerticalKnockback, items = __instance.itemKnockbackMultiplier
		};
		// Illegal statuses and blindness were never doubled by the old explosion.
		if (__state.status) __instance.statusAmount *= StatusScale;
		__instance.fallTime *= FallScale;
		__instance.knockback *= KnockbackScale;
		__instance.additionalVerticalKnockback *= KnockbackScale;
		__instance.itemKnockbackMultiplier /= KnockbackScale;
		explosionDepth++;
	}

	internal static System.Exception Finalizer(AOE __instance, Scaled __state, System.Exception __exception)
	{
		if (!__state.changed) return __exception;
		explosionDepth--;
		__instance.statusAmount = __state.statusAmount;
		__instance.fallTime = __state.fallTime;
		__instance.knockback = __state.knockback;
		__instance.additionalVerticalKnockback = __state.vertical;
		__instance.itemKnockbackMultiplier = __state.items;
		return __exception;
	}
}

// Native Explode calls Fall only for its owner's character; the old body added drowsiness right before that fall.
[HarmonyPatch(typeof(Character), "Fall")]
internal static class ExplosionFallDrowsiness
{
	private static void Prefix(Character __instance)
	{
		if (uberSplode.InExplosion && Rules.Enabled(9)) LegacyPenalties.ExplosionDrowsiness(__instance);
	}
}
