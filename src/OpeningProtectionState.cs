using System;
using System.Runtime.Serialization;

namespace dda;

[DataContract]
public sealed class OpeningProtectionState
{
    public const double Duration = 30;
    [DataMember] public string generation;
    [DataMember] public bool started;
    [DataMember] public double clock;

    public bool Valid => Guid.TryParseExact(generation, "N", out var id) && id != Guid.Empty &&
        WireJson.Finite(clock) && clock >= 0;

    public double Remaining(double now) => Valid && started && WireJson.Finite(now) && now >= 0
        ? Math.Max(0, Duration - WireJson.Elapsed(now, clock)) : 0;
}
