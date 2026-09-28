using System;
using System.Runtime.Serialization;

namespace dda;

public static class WeatherPolicy
{
    // TheKiln (4) is either Citadel or Kiln; later summit/void segments also stay clear.
    public static bool InWeatherSegment(int segment) => segment >= 0 && segment < 4;
}

[DataContract]
public sealed class SnowWeatherState
{
    [DataMember] public string runId;
    [DataMember] public double clock;
    [DataMember] public float duration;
    [DataMember] public bool active;
    [DataMember] public int direction;
    public bool Valid => Guid.TryParseExact(runId, "N", out var id) && id != Guid.Empty &&
        WireJson.Finite(clock) && clock >= 0 && WireJson.Finite(duration) &&
        // Accept the host's phase after crossing a region boundary or reconnecting.
        // The host chooses 30-90 normally and 60-120 in the tier-20 swamp.
        duration >= (active ? 15 : 30) && duration <= (active ? 25 : 120) && (direction == -1 || direction == 1);
    public double Elapsed(double now) => WireJson.Elapsed(now, clock);
    public double Remaining(double now) => Math.Max(0, duration - Elapsed(now));
}
