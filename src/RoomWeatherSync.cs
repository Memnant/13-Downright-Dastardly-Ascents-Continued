using System;
using System.Runtime.Serialization;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace dda;

// Native wind/rain/snow have different duration ranges. Keep those ranges on
// the host; serialize only the chosen phase, so peers never roll their own.
[DataContract]
public sealed class WeatherPhaseState
{
    [DataMember] public string runId;
    [DataMember] public double clock;
    [DataMember] public float duration;
    [DataMember] public bool active;
    [DataMember] public int direction;
    public bool Valid => Guid.TryParseExact(runId, "N", out var id) && id != Guid.Empty &&
        WireJson.Finite(clock) && clock >= 0 && WireJson.Finite(duration) && duration > 0 &&
        (direction == -1 || direction == 1);
    public double Elapsed(double now) => WireJson.Elapsed(now, clock);
    public double Remaining(double now) => Math.Max(0, duration - Elapsed(now));
}

internal static class RoomWeatherSync
{
    internal const string WindKey = "dda.continued.weather.wind.v1";
    internal const string SnowKey = "dda.continued.weather.snow.v1";
    internal const string RainKey = "dda.continued.weather.rain.v1";

    private sealed class Channel
    {
        internal readonly string Key;
        internal WeatherPhaseState State;
        internal string Received;
        internal Channel(string key) { Key = key; }
        internal void Reset() { State = null; Received = null; }
        internal void Receive(bool force)
        {
            string text = PhotonNetwork.CurrentRoom.CustomProperties[Key] as string;
            if (!force && text == Received) return;
            Received = text;
            State = null;
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                var next = WireJson.Decode<WeatherPhaseState>(text);
                if (next != null && next.Valid)
                {
                    State = next;
                    Report(Key, next, "received");
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Ignored invalid weather phase " + Key + ": " + e.Message); }
        }
    }

    private static readonly Channel wind = new(WindKey), snow = new(SnowKey), rain = new(RainKey);
    private static Channel For(WindChillZone zone) => zone.name == "WindStorm" ? wind :
        zone.name == "SnowStorm" ? snow : zone.name == "RainStorm" ? rain : null;
    internal static bool Handles(WindChillZone zone) => zone != null && Rules.Enabled(20) &&
        !EveryMapSnow.IsCreated(zone) && For(zone) != null;
    internal static void Reset() { wind.Reset(); snow.Reset(); rain.Reset(); }
    internal static void Receive(bool force = false)
    {
        if (!PhotonNetwork.InRoom || RunCoordinator.ResumePrepared || (RunCoordinator.IsHost && !force)) return;
        wind.Receive(force); snow.Receive(force); rain.Receive(force);
    }

    internal static void Apply(WindChillZone zone)
    {
        if (WeatherGrace.Suppress(zone)) return;
        var channel = For(zone);
        if (channel == null) return;
        if (!RunCoordinator.IsHost && PhotonNetwork.InRoom) channel.Receive(false);
        string run = TideSync.RunKey;
        double now = TideSync.Clock;
        var state = channel.State;
        if (RunCoordinator.IsHost && !RunCoordinator.ResumePrepared && TideSync.RunId != Guid.Empty &&
            (state == null || state.runId != run || state.Remaining(now) <= 0))
        {
            bool active = state == null || state.runId != run || !state.active;
            // This includes the existing swamp snow 60-120s rest override.
            float duration = zone.GetNextWindTime(active);
            // Native debug settings can request a zero rest. Avoid division by zero
            // without changing ordinary scene ranges or consuming phases per frame.
            if (!WireJson.Finite(duration) || duration <= 0) duration = 0.01f;
            state = new WeatherPhaseState { runId = run, clock = now, duration = duration,
                active = active, direction = UnityEngine.Random.value > .5f ? 1 : -1 };
            channel.State = state;
            Report(channel.Key, state, "host");
            if (PhotonNetwork.InRoom)
            {
                channel.Received = WireJson.Encode(state);
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [channel.Key] = channel.Received });
            }
        }
        ApplyState(zone, state != null && state.runId == run ? state : null, now);
    }

    // Once per phase/property change, never once per frame. Both logs can be
    // compared without recording player, lobby or Steam identifiers.
    private static void Report(string key, WeatherPhaseState state, string source) =>
        Plugin.Log.LogInfo($"Continued weather phase: {key}, {source}, active={state.active}, clock={state.clock:R}, duration={state.duration:R}, direction={state.direction}.");

    internal static void ApplyState(WindChillZone zone, WeatherPhaseState state, double now)
    {
        if (state == null || !state.Valid)
        {
            zone.windActive = false;
            zone.untilSwitch = zone.timeUntilNextWind = 1;
            zone.hasBeenActiveFor = 0;
            zone.currentForceMult = 0;
            zone.windIntensity = zone.windPlayerFactor = 0;
            return;
        }
        zone.windActive = state.active && state.Remaining(now) > 0;
        zone.untilSwitch = (float)state.Remaining(now);
        zone.timeUntilNextWind = state.duration;
        zone.currentWindDirection = Vector3.Lerp(Vector3.right * state.direction, Vector3.forward, .2f).normalized;
        zone.hasBeenActiveFor = zone.windActive ? Mathf.Max(0, (float)state.Elapsed(now) - Time.deltaTime) : 0;
        zone.currentForceMult = Mathf.Clamp01(zone.windActive ? (float)state.Elapsed(now) : 1f - (float)state.Elapsed(now));
        zone.StormProgress = zone.windActive ? Mathf.Clamp01(1 - zone.untilSwitch / state.duration) : 0;
        zone.timeUntilStorm = zone.windActive ? 0 : Mathf.Clamp01(1 - zone.untilSwitch / state.duration);
        zone.windIntensity = zone.windActive && zone.windIntensityCurve != null ? zone.windIntensityCurve.Evaluate(zone.StormProgress) : 0;
    }
}
