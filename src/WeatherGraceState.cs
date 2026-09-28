using System;
using System.Runtime.Serialization;

namespace dda;

[DataContract]
public sealed class WeatherGraceState
{
    public const double Duration = 30;
    [DataMember] public string generation;
    [DataMember] public string runId;
    [DataMember] public bool started;
    [DataMember] public double clock;

    public bool Valid => Guid.TryParseExact(generation, "N", out var token) && token != Guid.Empty &&
        WireJson.Finite(clock) && clock >= 0 &&
        ((!started && string.IsNullOrEmpty(runId)) ||
         (Guid.TryParseExact(runId, "N", out var id) && id != Guid.Empty));

    public bool TryStart(Guid run, double now)
    {
        if (!Valid || started || run == Guid.Empty || !WireJson.Finite(now) || now < 0 ||
            (!string.IsNullOrEmpty(runId) && runId != run.ToString("N"))) return false;
        runId = run.ToString("N");
        clock = now;
        started = true;
        return true;
    }

    public double Remaining(double now) => !Valid || !WireJson.Finite(now) || now < 0 ? 0 :
        !started ? Duration : Math.Max(0, Duration - WireJson.Elapsed(now, clock));

    public bool Blocks(string run, double now) => Valid &&
        (!started || string.IsNullOrEmpty(run) || run == "00000000000000000000000000000000" ||
         (runId == run && Remaining(now) > 0));
}
