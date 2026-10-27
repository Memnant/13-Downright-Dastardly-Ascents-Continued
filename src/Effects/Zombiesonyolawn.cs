// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;
using UnityEngine;

namespace dda;

[HarmonyPatch(typeof(MushroomZombieSpawner), "Spawn")]
internal static class Zombiesonyolawn
{
	internal static void Postfix(MushroomZombieSpawner __instance)
	{
		if (!(Rules.Enabled(9))) return;
		if (Rules.Enabled(9))
		{
			var zombie = __instance.spawnedZombie;
			if (zombie == null) return;
			EffectState.Set(zombie, nameof(zombie.attackHeightDelta), EffectState.Original(zombie, "zombie.height", zombie.attackHeightDelta) * 2f);
			EffectState.Set(zombie, nameof(zombie.biteStunTime), EffectState.Original(zombie, "zombie.stun", zombie.biteStunTime) * 2f);
			EffectState.Set(zombie, nameof(zombie.distanceBeforeWakeup), EffectState.Original(zombie, "zombie.wake", zombie.distanceBeforeWakeup) * 2f);
			EffectState.Set(zombie, nameof(zombie.distanceToEnable), EffectState.Original(zombie, "zombie.enable", zombie.distanceToEnable) * 2f);
			EffectState.Set(zombie, nameof(zombie.distanceBeforeChase), 0f);
			var movement = zombie.character.refs.movement;
			EffectState.Set(movement, nameof(movement.drag), 0.98f);
			EffectState.Set(movement, nameof(movement.movementForce),EffectState.Original(movement, "zombie.force", movement.movementForce) * 1.2f);
		}
	}
}
