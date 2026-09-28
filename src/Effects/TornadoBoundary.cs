using UnityEngine;

namespace dda;

internal static class TornadoBoundary
{
    // PEAK 2.4.c's real invisible-wall layer, independent of the tornado's own layer.
    private static readonly int WallMask = LayerMask.GetMask("InvisWall");
    private static readonly int GroundMask = LayerMask.GetMask("Terrain", "Map");
    private const float WallClearance = 12f;

    internal static Vector3 ClipToWalls(Vector3 origin, Vector3 desired)
    {
        var delta = desired - origin; delta.y = 0;
        float distance = delta.magnitude;
        if (distance < .001f) return origin;
        var direction = delta / distance;
        var rayOrigin = origin + Vector3.up * 2f;
        float travel = distance;
        if (Physics.Raycast(rayOrigin, direction, out var ray, distance + WallClearance,
            WallMask, QueryTriggerInteraction.Collide))
            travel = Mathf.Min(travel, Mathf.Max(0f, ray.distance - WallClearance));
        // The ray covers starting very close to a wall; the sphere covers corners
        // and oblique approaches so the tornado's capture area stays further inside.
        if (Physics.SphereCast(rayOrigin, WallClearance, direction, out var sphere, distance,
            WallMask, QueryTriggerInteraction.Collide))
            travel = Mathf.Min(travel, Mathf.Max(0f, sphere.distance - .1f));
        return origin + direction * travel;
    }

    internal static bool TryGround(Vector3 point, out Vector3 ground)
    {
        if (Physics.Raycast(point + Vector3.up * 200f, Vector3.down, out var hit,
            300f, GroundMask, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.point.y - point.y) <= 100f)
        {
            ground = hit.point;
            return true;
        }
        ground = point;
        return false;
    }

    internal static bool TryPlace(Vector3 origin, Vector3 desired, out Vector3 placed)
        => TryGround(ClipToWalls(origin, desired), out placed);

    internal static bool TrySpawn(Vector3 anchor, out Vector3 position)
        => TryRandomPosition(anchor, 200f, 30f, 12, out position);

    internal static bool TryTarget(Vector3 current, out Vector3 position)
        => TryRandomPosition(current, 150f, 2f, 8, out position);

    private static bool TryRandomPosition(Vector3 origin, float spread, float minDistance, int attempts, out Vector3 position)
    {
        for (int i = 0; i < attempts; i++)
        {
            var wanted = origin + new Vector3(Random.Range(-spread, spread), 0, Random.Range(-spread, spread));
            if (TryPlace(origin, wanted, out position))
            {
                var flat = position - origin; flat.y = 0;
                if (flat.sqrMagnitude >= minDistance * minDistance) return true;
            }
        }
        position = origin;
        return false;
    }

    internal static Vector3 Step(Vector3 current, Vector3 desired, float deltaTime, out bool blocked)
    {
        var clipped = ClipToWalls(current, desired);
        blocked = (clipped - desired).sqrMagnitude > .000001f;
        if (!TryGround(clipped, out var ground)) { blocked = true; return current; }
        clipped.y = Mathf.Lerp(current.y, ground.y, .5f * deltaTime);
        return clipped;
    }
}
