using System;
using System.Collections.Generic;

namespace dda;

public readonly struct ScoutPosition
{
    public readonly int Id;
    public readonly double X, Y, Z;
    public ScoutPosition(int id, double x, double y, double z) { Id = id; X = x; Y = y; Z = z; }
    public double Progress => Y + Math.Abs(Z);
    public double DistanceSquared(ScoutPosition other) =>
        (X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y) + (Z - other.Z) * (Z - other.Z);
}

public static class RuleZeroPolicy
{
    public static bool HasCompanion(IReadOnlyList<ScoutPosition> scouts, ScoutPosition target)
    {
        for (int i = 0; i < scouts.Count; i++)
        {
            var other = scouts[i];
            if (other.Id != target.Id && target.DistanceSquared(other) < 15 * 15) return true;
        }
        return false;
    }
    public static int PickTarget(IReadOnlyList<ScoutPosition> scouts)
    {
        int selected = -1;
        double best = double.NegativeInfinity;
        for (int i = 0; i < scouts.Count; i++)
        {
            var target = scouts[i];
            if (HasCompanion(scouts, target)) continue;
            for (int j = 0; j < scouts.Count; j++)
            {
                var other = scouts[j];
                if (other.Id != target.Id && target.Progress > other.Progress + 150 &&
                    (target.Progress > best || (target.Progress == best && target.Id < selected)))
                { best = target.Progress; selected = target.Id; }
            }
        }
        return selected;
    }
    public static bool ShouldRelease(IReadOnlyList<ScoutPosition> scouts, int targetId)
    {
        if (scouts.Count < 2) return true;
        for (int i = 0; i < scouts.Count; i++)
        {
            var target = scouts[i];
            if (target.Id == targetId) return HasCompanion(scouts, target);
        }
        return true;
    }
}
