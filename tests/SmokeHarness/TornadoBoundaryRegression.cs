using System;
using System.Reflection;
using UnityEngine;

internal static class TornadoBoundaryRegression
{
    internal static int Run(Assembly mod)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        var boundary = mod.GetType("dda.TornadoBoundary");
        var place = boundary.GetMethod("TryPlace", flags);
        var step = boundary.GetMethod("Step", flags);
        var spawn = boundary.GetMethod("TrySpawn", flags);
        var target = boundary.GetMethod("TryTarget", flags);
        var root = new GameObject("Continued boundary physics fixture");
        var center = new Vector3(10000, 0, 10000);
        var randomState = UnityEngine.Random.state;
        int checks = 0;
        try
        {
            AddBox("ground", "Terrain", center + Vector3.down, new Vector3(200, 2, 200));
            var wall = AddBox("invisible wall", "InvisWall", center + Vector3.right * 50 + Vector3.up * 80, new Vector3(2, 200, 300));
            Physics.SyncTransforms();
            var start = center + Vector3.up * 20;
            object[] args = { start, start + Vector3.right * 180, null };
            Assert((bool)place.Invoke(null, args), "spawn outside wall is projected onto real interior ground");
            var clipped = (Vector3)args[2];
            Assert(clipped.x < center.x + 38 && Math.Abs(clipped.y) < .01, "spawn keeps clearance from invisible wall");
            args = new object[] { start, start + Vector3.left * 180, null };
            Assert(!(bool)place.Invoke(null, args), "spawn without ground is rejected");
            args = new object[] { start, start + Vector3.forward * 30, null };
            Assert((bool)place.Invoke(null, args) && ((Vector3)args[2]).z == center.z + 30, "interior candidate is not clamped unnecessarily");
            foreach (int fps in new[] { 30, 60, 144 })
            {
                var position = start;
                bool blocked = false;
                for (int i = 0; i < fps * 12; i++)
                {
                    object[] tick = { position, position + Vector3.right * (7.5f / fps), 1f / fps, false };
                    position = (Vector3)step.Invoke(null, tick);
                    blocked |= (bool)tick[3];
                }
                Assert(blocked && position.x < center.x + 38, "movement cannot cross wall at " + fps + " FPS");
            }
            object[] hitch = { start, start + Vector3.right * 300, 2f, false };
            Assert(((Vector3)step.Invoke(null, hitch)).x < center.x + 38 && (bool)hitch[3], "long frame cannot tunnel through wall");
            object[] edge = { start, start + Vector3.left * 180, .02f, false };
            Assert((Vector3)step.Invoke(null, edge) == start && (bool)edge[3], "missing ground stops movement instead of drifting off map");
            wall.transform.rotation = Quaternion.Euler(0, 25, 0); Physics.SyncTransforms();
            args = new object[] { start, start + Vector3.right * 180, null };
            Assert((bool)place.Invoke(null, args) && ((Vector3)args[2]).x < center.x + 50, "rotated wall also blocks crossing");
            wall.enabled = false; Physics.SyncTransforms();
            args = new object[] { start, start + Vector3.right * 80, null };
            Assert((bool)place.Invoke(null, args) && ((Vector3)args[2]).x == center.x + 80, "disabled segment wall does not block weather");
            wall.enabled = true; wall.transform.rotation = Quaternion.identity; Physics.SyncTransforms();
            UnityEngine.Random.InitState(156);
            foreach (var method in new[] { spawn, target })
            for (int i = 0; i < 20; i++)
            {
                object[] chosen = { start, null };
                bool found = (bool)method.Invoke(null, chosen);
                var value = (Vector3)chosen[1];
                Assert(!found || (value.x < center.x + 38 && Mathf.Abs(value.y) < .01f &&
                    Mathf.Abs(value.x - center.x) <= 100 && Mathf.Abs(value.z - center.z) <= 100), "random point is on interior ground");
            }
            return checks;
        }
        finally { UnityEngine.Random.state = randomState; UnityEngine.Object.DestroyImmediate(root); }
        BoxCollider AddBox(string name, string layer, Vector3 position, Vector3 size)
        {
            var child = new GameObject(name); child.transform.SetParent(root.transform); child.transform.position = position;
            child.layer = LayerMask.NameToLayer(layer);
            var box = child.AddComponent<BoxCollider>(); box.size = size; return box;
        }
        void Assert(bool pass, string reason) { if (!pass) throw new Exception(reason); checks++; }
    }
}
