using System;
using System.Collections.Generic;
using HarmonyLib;
using Peak;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace dda;

internal static class OpeningProtection
{
    internal const string RoomKey = "dda.continued.opening.v1";
    private static OpeningProtectionState state;
    private static string received;
    private static readonly List<Character> protectedCharacters = new(16);
    internal static double Remaining => Plugin.Ready && RunCoordinator.InRun && !RunCoordinator.ResumePrepared &&
        Rules.Level == 20 && state != null ? state.Remaining(TideSync.Clock) : 0;

    // Only an airport departure arms a new window. Joining a room never arms one.
    internal static void Arm()
    {
        if (!RunCoordinator.IsHost) return;
        state = Rules.Level == 20 ? new OpeningProtectionState { generation = Guid.NewGuid().ToString("N") } : null;
        Publish();
    }

    internal static void Start()
    {
        if (!RunCoordinator.IsHost || Rules.Level != 20 || RunCoordinator.ResumePrepared ||
            Quicksave.ShouldUseSaveData || state == null || !state.Valid || state.started) return;
        state.started = true;
        state.clock = TideSync.Clock;
        Publish();
        Tick();
        Plugin.Log.LogInfo("Ascent 20 opening protection started: shared 30-second window.");
    }

    private static void Publish()
    {
        received = state == null ? null : WireJson.Encode(state);
        if (PhotonNetwork.InRoom && RunCoordinator.IsHost)
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [RoomKey] = received });
    }

    internal static void Receive(bool force = false)
    {
        if (!PhotonNetwork.InRoom || (RunCoordinator.IsHost && !force)) return;
        string text = PhotonNetwork.CurrentRoom.CustomProperties[RoomKey] as string;
        if (!force && received == text) return;
        received = text;
        state = null;
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            var next = WireJson.Decode<OpeningProtectionState>(text);
            if (next != null && next.Valid) state = next;
        }
        catch (Exception error) { Plugin.Log.LogWarning("Ignored invalid opening protection: " + error.Message); }
    }

    internal static bool Protects(Character character) => Remaining > 0 && character != null &&
        character.data != null && character.refs?.afflictions != null && !character.inAirport &&
        !character.isBot && !character.isScoutmaster && !character.data.dead;

    internal static void Tick()
    {
        Receive();
        // A host can leave while the level is loading, after the successor already
        // ran StartRun as a client. Start its inherited, still-armed window once ready.
        if (state != null && !state.started && RunCoordinator.IsHost && RunManager.Instance != null &&
            RunManager.Instance.runStarted && !LoadingScreenHandler.loading && Character.localCharacter != null &&
            Character.localCharacter.refs?.afflictions != null && !Character.localCharacter.inAirport)
            Start();
        for (int i = protectedCharacters.Count - 1; i >= 0; i--)
        {
            var character = protectedCharacters[i];
            if (Protects(character)) continue;
            protectedCharacters.RemoveAt(i);
            if (character != null && character.data != null && character.refs?.afflictions != null)
                character.data.RecalculateInvincibility();
        }
        if (Remaining <= 0) return;
        foreach (var character in Character.AllCharacters)
        {
            if (!Protects(character) || protectedCharacters.Contains(character) || character.refs?.afflictions == null) continue;
            protectedCharacters.Add(character);
            character.data.RecalculateInvincibility();
        }
    }

    internal static void Reset()
    {
        state = null;
        received = null;
        foreach (var character in protectedCharacters)
            if (character != null && character.data != null && character.refs?.afflictions != null)
                character.data.RecalculateInvincibility();
        protectedCharacters.Clear();
    }

    internal static void DisableForResume()
    {
        Reset();
        if (RunCoordinator.IsHost) Publish();
    }
}

[HarmonyPatch(typeof(RunManager), "StartRun")]
internal static class StartOpeningProtection
{
    private static void Postfix() => OpeningProtection.Start();
}

[HarmonyPatch(typeof(CharacterData), "RecalculateInvincibility")]
internal static class PreserveOpeningProtection
{
    private static void Postfix(CharacterData __instance)
    {
        // Original afflictions are recalculated first. No timer or stack is overwritten.
        if (OpeningProtection.Protects(__instance.character)) __instance.isInvincible = true;
    }
}
