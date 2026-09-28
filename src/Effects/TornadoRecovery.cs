using System;
using HarmonyLib;
using UnityEngine;

namespace dda;

// Local physics are owned by this client's scout. No RPC or save state is added:
// capture, emote and equipment still travel through the game's original network paths.
internal static class TornadoRecovery
{
    internal const string PlayDeadEmote = "A_Scout_Emote_PlayDead";
    private static Character tracked;
    private static Guid run;
    private static bool airborne, playedDead;
    private static Item recoveryItem;
    private static float equipUntil;
    [ThreadStatic] internal static Character PlayDeadScope;

    internal static void ObserveCapture(Tornado tornado)
    {
        var character = Character.localCharacter;
        if (!Rules.Enabled(13) || character == null || character.data == null || !character.data.fullyConscious ||
            TideSync.RunId == Guid.Empty || !tornado.caughtCharacters.Contains(character) ||
            tornado.ignoredCharacters.Contains(character)) return;
        Tick();
        // Wait for an airborne capture tick. A capture that ends on the ground
        // must not leave eligibility for a later, unrelated jump off a cliff.
        if (character.data.isGrounded) return;
        if (tracked != character)
        {
            Reset(); tracked = character; run = TideSync.RunId;
        }
        airborne = true;
    }

    internal static void Tick()
    {
        if (tracked == null) return;
        if (!Rules.Enabled(13) || tracked != Character.localCharacter || run != TideSync.RunId ||
            tracked.data == null || tracked.refs?.afflictions == null || tracked.inAirport || tracked.warping ||
            !tracked.data.fullyConscious || tracked.data.isClimbing || tracked.data.isRopeClimbing ||
            tracked.data.isVineClimbing || tracked.data.currentClimbHandle != null ||
            (airborne && tracked.data.isGrounded))
        { Reset(); return; }
        if (!tracked.data.isGrounded) airborne = true;
    }

    internal static bool BeginPlayDead(Character character)
    {
        Tick();
        if (character == null || character != tracked || !airborne) return false;
        playedDead = true;
        recoveryItem = null;
        return true;
    }

    internal static void Equip(Character character, Item item)
    {
        Tick();
        if (character == null || character != tracked) return;
        recoveryItem = playedDead && airborne && character.refs?.hip != null ? item : null;
        // Native Equip aligns the ragdoll immediately and over three fixed frames.
        // Permit only that brief alignment, while the newly equipped item is still held.
        equipUntil = Time.fixedTime + Time.fixedDeltaTime * 5f;
    }

    internal static bool AllowsSnap(Character character)
    {
        Tick();
        return character != null && character == tracked && airborne && playedDead && recoveryItem != null &&
            character.data.currentItem == recoveryItem && Time.fixedTime <= equipUntil;
    }

    internal static bool SkipPlayDeadLock(Character character) => character != null && character == PlayDeadScope;
    internal static void PlayDeadFailed(Character character)
    { if (character == tracked) { playedDead = false; recoveryItem = null; } }
    internal static void EquipFailed(Character character) { if (character == tracked) recoveryItem = null; }
    internal static void Forget(Character character) { if (character == tracked) Reset(); }
    internal static void Reset()
    { tracked = null; run = Guid.Empty; airborne = playedDead = false; recoveryItem = null; equipUntil = 0; }
}

[HarmonyPatch(typeof(Tornado), "CapturedCharacter")]
internal static class TrackTornadoRecovery
{
    // HarmonyX runs this observer even when the old force patch replaces the body.
    // Observe before the force patch can release and remove a captured character.
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Tornado __instance) => TornadoRecovery.ObserveCapture(__instance);
}

[HarmonyPatch(typeof(CharacterAnimations), "RPCA_PlayRemove")]
internal static class TornadoPlayDead
{
    private static void Prefix(CharacterAnimations __instance, string emoteName, out Character __state)
    {
        __state = TornadoRecovery.PlayDeadScope;
        TornadoRecovery.PlayDeadScope = emoteName == TornadoRecovery.PlayDeadEmote &&
            TornadoRecovery.BeginPlayDead(__instance.character) ? __instance.character : null;
    }
    private static Exception Finalizer(CharacterAnimations __instance, Exception __exception, Character __state)
    {
        if (__exception != null) TornadoRecovery.PlayDeadFailed(__instance.character);
        TornadoRecovery.PlayDeadScope = __state;
        return __exception;
    }
}

[HarmonyPatch(typeof(CharacterItems), "LockFromSwitching")]
internal static class TornadoPlayDeadLock
{
    // Leave existing cooldowns intact; only omit the new delay inside this play-dead call.
    private static bool Prefix(CharacterItems __instance) => !TornadoRecovery.SkipPlayDeadLock(__instance.character);
}

[HarmonyPatch(typeof(CharacterItems), "Equip")]
internal static class TornadoEquipRecovery
{
    private static void Prefix(CharacterItems __instance, Item item) => TornadoRecovery.Equip(__instance.character, item);
    private static Exception Finalizer(CharacterItems __instance, Exception __exception)
    {
        if (__exception != null) TornadoRecovery.EquipFailed(__instance.character);
        return __exception;
    }
}

[HarmonyPatch(typeof(Character), "OnLand")]
internal static class EndTornadoRecovery
{
    private static void Postfix(Character __instance) => TornadoRecovery.Forget(__instance);
}
