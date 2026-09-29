using System;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Table = ExitGames.Client.Photon.Hashtable;

// Calls patched native HandleTime/RPCA_ToggleWind with room property containers.
// Transport, role and clock are simulated; no second process or live room.
internal static class NativeWeatherSyncRegression
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static Room room;
    private static bool host;
    private static double now;
    private static Guid run;
    private static int publications;

    internal static int Run(Assembly mod)
    {
        var coordinator = mod.GetType("dda.RunCoordinator");
        var grace = mod.GetType("dda.WeatherGrace");
        var sync = mod.GetType("dda.RoomWeatherSync");
        var fields = new[] { coordinator.GetField("Active", F), coordinator.GetField("InRun", F),
            coordinator.GetField("ResumePrepared", F), grace.GetField("state", F), grace.GetField("received", F) };
        var saved = new object[fields.Length];
        for (int i = 0; i < fields.Length; i++) saved[i] = fields[i].GetValue(null);
        var patches = new Harmony("dda.continued.native-weather-fixture");
        var root = new GameObject("Weather clock fixture"); root.SetActive(false);
        var peerRoot = new GameObject("Weather peer fixture"); peerRoot.SetActive(false);
        int checks = 0;
        try
        {
            room = new Room("synthetic-weather-room", new RoomOptions());
            run = Guid.NewGuid(); now = 100; publications = 0;
            Patch(AccessTools.PropertyGetter(typeof(PhotonNetwork), "InRoom"), nameof(InRoom));
            Patch(AccessTools.PropertyGetter(typeof(PhotonNetwork), "CurrentRoom"), nameof(CurrentRoom));
            Patch(AccessTools.PropertyGetter(typeof(PhotonNetwork), "IsMasterClient"), nameof(IsHost));
            Patch(AccessTools.PropertyGetter(coordinator, "IsHost"), nameof(IsHost));
            Patch(AccessTools.PropertyGetter(mod.GetType("dda.TideSync"), "RunId"), nameof(RunId));
            Patch(AccessTools.PropertyGetter(mod.GetType("dda.TideSync"), "Clock"), nameof(Clock));
            Patch(AccessTools.Method(typeof(Room), "SetCustomProperties"), nameof(RoomSet));
            fields[0].SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), 20, 0, true, true));
            fields[1].SetValue(null, true); fields[2].SetValue(null, false);
            var graceState = Activator.CreateInstance(mod.GetType("dda.WeatherGraceState"));
            Set(graceState, "generation", Guid.NewGuid().ToString("N")); Set(graceState, "runId", run.ToString("N"));
            Set(graceState, "started", true); Set(graceState, "clock", 1d); fields[3].SetValue(null, graceState);
            var zoneType = AccessTools.TypeByName("WindChillZone");
            var zone = root.AddComponent(zoneType); var peer = peerRoot.AddComponent(zoneType);
            var handle = zoneType.GetMethod("HandleTime", F); var rpc = zoneType.GetMethod("RPCA_ToggleWind", F);
            var reset = sync.GetMethod("Reset", F); var receive = sync.GetMethod("Receive", F);
            foreach (string name in new[] { "WindStorm", "SnowStorm", "RainStorm" })
            {
                root.name = peerRoot.name = name; reset.Invoke(null, null); room.CustomProperties.Clear();
                now = 100; host = true;
                var on = name == "SnowStorm" ? new Vector2(15, 25) : name == "WindStorm" ? new Vector2(7.5f, 15) : new Vector2(20, 40);
                var off = name == "RainStorm" ? new Vector2(40, 80) : new Vector2(30, 90);
                Set(zone, "windTimeRangeOn", on); Set(zone, "windTimeRangeOff", off);
                // A peer's local ranges deliberately disagree; they must never choose the phase.
                Set(peer, "windTimeRangeOn", new Vector2(1000, 1000)); Set(peer, "windTimeRangeOff", new Vector2(1000, 1000));
                Set(zone, "windIntensityCurve", AnimationCurve.Linear(0, 0, 1, 1));
                Set(peer, "windIntensityCurve", AnimationCurve.Linear(0, 0, 1, 1));
                handle.Invoke(zone, null);
                float duration = (float)Get(zone, "timeUntilNextWind");
                string key = (string)sync.GetField(name == "WindStorm" ? "WindKey" : name == "SnowStorm" ? "SnowKey" : "RainKey", F).GetValue(null);
                string encoded = room.CustomProperties[key] as string;
                Assert((bool)Get(zone, "windActive") && duration >= on.x && duration <= on.y && encoded != null,
                    name + ": host uses existing active range and saves phase in room");
                int sent = publications;
                now = 105; handle.Invoke(zone, null);
                Assert(publications == sent && (string)room.CustomProperties[key] == encoded, "same phase never republishes per frame");
                host = false; reset.Invoke(null, null); // A newly joined client has no local cache or old RPC.
                handle.Invoke(peer, null);
                Same("late join restores current phase, time, direction and force ramp");
                rpc.Invoke(peer, new object[] { false, Vector3.back, 900f });
                Same("stale native RPC cannot overwrite the room phase");
                reset.Invoke(null, null); receive.Invoke(null, new object[] { false }); handle.Invoke(peer, null);
                Same("reconnect restores phase without restarting");
                host = true; reset.Invoke(null, null); receive.Invoke(null, new object[] { true }); handle.Invoke(peer, null);
                Assert(publications == sent && (string)room.CustomProperties[key] == encoded, "new host inherits deadline without a fresh random roll");
                now = 100 + duration + .01; handle.Invoke(zone, null);
                float rest = (float)Get(zone, "timeUntilNextWind");
                Assert(!(bool)Get(zone, "windActive") && rest >= off.x && rest <= off.y, "host retains existing rest range");
                host = false; handle.Invoke(peer, null); Same("client adopts next rest phase");
                string restText = (string)room.CustomProperties[key];
                now += rest + 1; handle.Invoke(peer, null);
                Assert(!(bool)Get(peer, "windActive") && (string)room.CustomProperties[key] == restText, "client cannot invent a new phase while awaiting host");
                run = Guid.NewGuid(); handle.Invoke(peer, null);
                Assert(!(bool)Get(peer, "windActive"), "another RunId cannot inherit old weather");
                room.CustomProperties[key] = "invalid"; receive.Invoke(null, new object[] { false }); handle.Invoke(peer, null);
                Assert(!(bool)Get(peer, "windActive"), "invalid weather metadata fails calm");
                // Current run changed above; the expired grace for the old run is nonblocking.
            }
            fields[0].SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), 19, 0, true, true));
            rpc.Invoke(peer, new object[] { true, Vector3.forward, 20f });
            Assert((bool)Get(peer, "windActive"), "tier 19 keeps original weather RPC authority");
            fields[0].SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), 0, 1 << 11, true, true));
            Assert((bool)sync.GetMethod("Handles", F).Invoke(null, new[] { peer }), "forced tier 20 uses shared weather too");
            return checks;

            void Same(string reason)
            {
                Assert(Equals(Get(zone, "windActive"), Get(peer, "windActive")) &&
                    Math.Abs((float)Get(zone, "untilSwitch") - (float)Get(peer, "untilSwitch")) < .0001f &&
                    (Vector3)Get(zone, "currentWindDirection") == (Vector3)Get(peer, "currentWindDirection") &&
                    Math.Abs((float)Get(zone, "currentForceMult") - (float)Get(peer, "currentForceMult")) < .0001f &&
                    Math.Abs((float)Get(zone, "windIntensity") - (float)Get(peer, "windIntensity")) < .0001f, reason);
            }
        }
        finally
        {
            sync.GetMethod("Reset", F).Invoke(null, null);
            patches.UnpatchSelf();
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, saved[i]);
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(peerRoot); room = null;
        }
        void Patch(MethodInfo target, string name) => patches.Patch(target, prefix: new HarmonyMethod(typeof(NativeWeatherSyncRegression), name));
        void Assert(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    }
    private static object Get(object x, string name) => AccessTools.Field(x.GetType(), name).GetValue(x);
    private static void Set(object x, string name, object value) => AccessTools.Field(x.GetType(), name).SetValue(x, value);
    private static bool InRoom(ref bool __result) { __result = true; return false; }
    private static bool CurrentRoom(ref Room __result) { __result = room; return false; }
    private static bool IsHost(ref bool __result) { __result = host; return false; }
    private static bool RunId(ref Guid __result) { __result = run; return false; }
    private static bool Clock(ref double __result) { __result = now; return false; }
    private static bool RoomSet(Room __instance, Table propertiesToSet, ref bool __result)
    { foreach (var key in propertiesToSet.Keys) __instance.CustomProperties[key] = propertiesToSet[key]; publications++; __result = true; return false; }
}
