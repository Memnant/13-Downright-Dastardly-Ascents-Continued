using System;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace dda;

internal static class SnowWeatherSync
{
    internal const string RoomKey = "dda.continued.snow.v1";
    private static SnowWeatherState state;
    private static string received;
    internal static void Reset() { state = null; received = null; }
    internal static void Receive(bool force = false)
    {
        if (!PhotonNetwork.InRoom || RunCoordinator.ResumePrepared || (RunCoordinator.IsHost && !force)) return;
        string encoded = PhotonNetwork.CurrentRoom.CustomProperties[RoomKey] as string;
        if (!force && encoded == received) return;
        received = encoded;
        if (string.IsNullOrEmpty(encoded)) { state = null; return; }
        try
        {
            var next = WireJson.Decode<SnowWeatherState>(encoded);
            if (next != null && next.Valid) state = next;
        }
        catch (Exception e) { Plugin.Log.LogWarning("Ignored invalid snow weather state: " + e.Message); }
    }
    internal static void Apply(WindChillZone zone)
    {
        if (WeatherGrace.Suppress(zone)) return;
        if (!RunCoordinator.IsHost) Receive();
        string run = TideSync.RunKey;
        if (RunCoordinator.IsHost && !RunCoordinator.ResumePrepared && TideSync.RunId != Guid.Empty &&
            (state == null || state.runId != run || state.Remaining(TideSync.Clock) <= 0))
        {
            bool active = state == null || state.runId != run || !state.active;
            state = new SnowWeatherState { runId = run, clock = TideSync.Clock, active = active,
                duration = active ? UnityEngine.Random.Range(15f, 25f) : Tier20Swamp.NextSnowRest(),
                direction = UnityEngine.Random.value > .5f ? 1 : -1 };
            if (PhotonNetwork.InRoom)
            {
                received = WireJson.Encode(state);
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [RoomKey] = received });
            }
        }
        ApplyState(zone, state != null && state.runId == run ? state : null, TideSync.Clock);
    }
    internal static void ApplyState(WindChillZone zone, SnowWeatherState value, double now)
    {
        if (value == null || !value.Valid)
        {
            zone.windActive = false; zone.untilSwitch = zone.timeUntilNextWind = 1;
            zone.hasBeenActiveFor = 0;
            zone.currentForceMult = 0;
            zone.windIntensity = zone.windPlayerFactor = 0;
            return;
        }
        zone.windActive = value.active && value.Remaining(now) > 0;
        zone.untilSwitch = (float)value.Remaining(now);
        zone.timeUntilNextWind = value.duration;
        zone.currentWindDirection = Vector3.Lerp(Vector3.right * value.direction, Vector3.forward, .2f).normalized;
        // Native Update adds one deltaTime after HandleTime.
        zone.hasBeenActiveFor = zone.windActive ? Mathf.Max(0, (float)value.Elapsed(now) - Time.deltaTime) : 0;
        zone.currentForceMult = Mathf.Clamp01(zone.windActive ? (float)value.Elapsed(now) : 1f - (float)value.Elapsed(now));
        zone.StormProgress = zone.windActive ? Mathf.Clamp01(1 - zone.untilSwitch / value.duration) : 0;
        zone.timeUntilStorm = zone.windActive ? 0 : Mathf.Clamp01(1 - zone.untilSwitch / value.duration);
        zone.windIntensity = zone.windActive && zone.windIntensityCurve != null ? zone.windIntensityCurve.Evaluate(zone.StormProgress) : 0;
    }
}
