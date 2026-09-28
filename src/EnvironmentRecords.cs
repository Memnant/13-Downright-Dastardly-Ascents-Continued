using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace dda;

public static class WireJson
{
    public static string Encode<T>(T value)
    {
        using var stream = new MemoryStream();
        new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    public static T Decode<T>(string value)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(value));
        return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
    }
    public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    public static double Elapsed(double now, double then)
    {
        double delta = now - then;
        if (delta < -2147483.648) delta += 4294967.296; // Photon timestamp rollover.
        return Math.Max(0, delta);
    }
}

[DataContract]
public sealed class TideAnchor
{
    [DataMember] public string runId;
    [DataMember] public double phase;
    [DataMember] public double clock;
    [DataMember] public bool running;
    public bool Valid => Guid.TryParseExact(runId, "N", out var id) && id != Guid.Empty &&
        VolcanoTide.ValidPhase(phase) && WireJson.Finite(clock) && clock >= 0;
    public double At(double now) => running ? VolcanoTide.Advance(phase, WireJson.Elapsed(now, clock)) : phase;
}

[DataContract]
public sealed class ScoutmasterLease
{
    [DataMember] public string runId;
    [DataMember] public string generation;
    [DataMember] public int sequence;
    [DataMember] public int scoutId;
    [DataMember] public int targetId;
    [DataMember] public bool active;
    [DataMember] public double clock;
    public bool Valid => Guid.TryParseExact(runId, "N", out var id) && id != Guid.Empty &&
        Guid.TryParseExact(generation, "N", out _) && sequence > 0 && scoutId > 0 && targetId > 0 &&
        WireJson.Finite(clock) && clock >= 0;
}
