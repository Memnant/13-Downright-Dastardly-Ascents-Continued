using System;

namespace dda;

/// <summary>The legacy curve expressed in elapsed seconds, independent of ticks and object count.</summary>
public static class VolcanoTide
{
    public const double MinimumPhase = -0.35;
    public static readonly double MaximumPhase = Math.Sqrt(2 * Math.PI);
    private const double SecondsPerPhase = 17.8;
    private const double SlowStart = 1.75, SlowEnd = 1.85, SlowSpeed = 0.15;
    public static double Period => PhaseTime(MaximumPhase);
    public static bool ValidPhase(double phase) => !double.IsNaN(phase) && !double.IsInfinity(phase) &&
        phase >= MinimumPhase && phase <= MaximumPhase;
    private static double PhaseTime(double phase) => SecondsPerPhase *
        (phase - MinimumPhase + Math.Max(0, Math.Min(phase, SlowEnd) - SlowStart) * (1 / SlowSpeed - 1));
    public static double Advance(double phase, double elapsed)
    {
        if (!ValidPhase(phase) || double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0)
            throw new ArgumentOutOfRangeException();
        double time = (PhaseTime(phase) + elapsed) % Period;
        double slowBegin = PhaseTime(SlowStart), slowFinish = PhaseTime(SlowEnd);
        if (time < slowBegin) return MinimumPhase + time / SecondsPerPhase;
        if (time < slowFinish) return SlowStart + (time - slowBegin) * SlowSpeed / SecondsPerPhase;
        return SlowEnd + (time - slowFinish) / SecondsPerPhase;
    }
    public static double Height(double phase) => 787 - 8 * Math.Cos(phase * phase);
}
