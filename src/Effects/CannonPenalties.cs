using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Photon.Pun;

namespace dda;

internal sealed class CannonShot
{
    private static readonly ConditionalWeakTable<ScoutCannon, CannonShot> Shots = new();
    internal static CannonShot For(ScoutCannon cannon) => Shots.GetOrCreateValue(cannon);
    internal readonly HashSet<int> Launched = new();
    internal bool consumed;
}

[HarmonyPatch(typeof(ScoutCannon), "RPCA_Light")]
internal static class ContinuedCannonShot
{
    private static void Prefix(ScoutCannon __instance)
    {
        var shot = CannonShot.For(__instance);
        shot.Launched.Clear();
        shot.consumed = false;
    }
}

[HarmonyPatch(typeof(ScoutCannon), "RPCA_LaunchTarget")]
internal static class hotShot
{
    private static void Postfix(ScoutCannon __instance, int targetID)
    {
        if (!Rules.Enabled(12)) return;
        var view = PhotonView.Find(targetID);
        var character = view == null ? null : view.GetComponent<Character>();
        if (character == null || !CannonShot.For(__instance).Launched.Add(targetID)) return;
        if (Rules.Owns(character)) character.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Hot, 0.5f);
    }
}

[HarmonyPatch(typeof(ScoutCannon), "RPCA_LaunchItem")]
internal static class DIE
{
    private static void Postfix(ScoutCannon __instance, int targetID)
    {
        if (!Rules.Enabled(12)) return;
        var shot = CannonShot.For(__instance);
        var view = PhotonView.Find(targetID);
        if (shot.consumed || shot.Launched.Count == 0 || view == null || view.GetComponent<Item>() == null) return;
        shot.consumed = true;
        // Original mixed player/item cannon penalty, once per shot on each player's owner.
        foreach (var character in Character.AllCharacters)
        {
            if (!Rules.Owns(character)) continue;
            character.refs.afflictions.SetStatus(CharacterAfflictions.STATUSTYPE.Hot, 0f);
            character.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Crab, 200f);
            character.PassOutInstantly();
        }
    }
}
