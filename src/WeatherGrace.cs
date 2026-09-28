using System;
using HarmonyLib;
using Photon.Pun;
using Zorro.Core;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace dda;

internal static class WeatherGrace
{
    internal const string RoomKey = "dda.continued.weatherGrace.v1";
    private static WeatherGraceState state;
    private static string received;

    internal static bool Blocking => Rules.Enabled(20) &&
        (RunCoordinator.ResumePrepared || (state != null ? state.Blocks(TideSync.RunKey, TideSync.Clock) :
            PhotonNetwork.InRoom && !RunCoordinator.IsHost));

    // Called only for a fresh airport departure or a native checkpoint load, never on join.
    // A resumed run's identity is known before the old scene and room have been left.
    internal static void Arm(Guid resumedRun = default)
    {
        if (!RunCoordinator.IsHost) return;
        state = Rules.Enabled(20) ? new WeatherGraceState {
            generation = Guid.NewGuid().ToString("N"),
            runId = resumedRun == Guid.Empty ? null : resumedRun.ToString("N")
        } : null;
        Publish();
    }

    private static void Publish()
    {
        received = state == null ? null : WireJson.Encode(state);
        if (PhotonNetwork.InRoom && RunCoordinator.IsHost)
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [RoomKey] = received });
    }

    internal static void JoinedRoom()
    {
        // Offline/native resume can recreate the room after Arm. Republish the pending
        // window instead of replacing it with the new room's initially empty property.
        if (RunCoordinator.IsHost && state != null) Publish();
        else Receive(true);
    }

    internal static void Receive(bool force = false)
    {
        if (!PhotonNetwork.InRoom || RunCoordinator.ResumePrepared || (RunCoordinator.IsHost && !force)) return;
        string text = PhotonNetwork.CurrentRoom.CustomProperties[RoomKey] as string;
        if (!force && text == received) return;
        received = text;
        state = null;
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            var next = WireJson.Decode<WeatherGraceState>(text);
            if (next != null && next.Valid) state = next;
        }
        catch (Exception error) { Plugin.Log.LogWarning("Ignored invalid weather grace: " + error.Message); }
    }

    internal static bool ReadyToStart()
    {
        var character = Character.localCharacter;
        return !RunCoordinator.ResumePrepared && !LoadingScreenHandler.loading &&
            RunManager.Instance != null && RunManager.Instance.runStarted && TideSync.RunId != Guid.Empty &&
            MapHandler.Exists && Singleton<MapHandler>.Instance.hasFinishedStartRoutine &&
            character != null && character.data != null && character.refs?.afflictions != null &&
            !character.inAirport && !character.warping &&
            character.data.passedOutOnTheBeach <= 0;
    }

    internal static void Tick()
    {
        if (!Rules.Enabled(20)) return;
        Receive();
        if (RunCoordinator.IsHost && state != null && !state.started && ReadyToStart() &&
            state.TryStart(TideSync.RunId, TideSync.Clock))
        {
            Publish();
            Plugin.Log.LogInfo("Ascent 20 weather grace started after wake/resume: shared 30 seconds. Tornado rules unchanged.");
        }
    }

    internal static bool Suppress(WindChillZone zone)
    {
        if (zone == null || !Blocking) return false;
        // Suppress the source, including native physical/item forces and status effects.
        // Visuals/audio consume these same flags. Do not clear character ailments or velocity.
        zone.windActive = false;
        zone.localCharacterInsideBounds = zone.observedCharacterInsideBounds = false;
        zone.windPlayerFactor = zone.windIntensity = zone.hasBeenActiveFor = zone.currentForceMult = 0;
        zone.StormProgress = zone.timeUntilStorm = 0;
        // No random weather phases are consumed while waiting. The native host can start
        // the first normal cycle after expiry; synthetic snow uses its shared clock.
        zone.untilSwitch = 0;
        zone.timeUntilNextWind = 1;
        return true;
    }

    internal static void Reset() { state = null; received = null; }
}

// A queued RPC must not turn visuals/forces back on between Update and LateUpdate.
[HarmonyPatch(typeof(WindChillZone), "RPCA_ToggleWind")]
internal static class ContinuedWeatherGraceRpc
{
    private static bool Prefix(WindChillZone __instance) => !WeatherGrace.Suppress(__instance);
}
