using System;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Real coordinator and Photon property containers, synthetic transport/scene state.
// No Steam session or second game client is created by this fixture.
internal static class ClientRulesRegression
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static Room room;
    private static Player player;
    private static bool menu, host;
    private static int baseAscent = 8;

    internal static int Run(Assembly mod)
    {
        var coordinator = mod.GetType("dda.RunCoordinator");
        var fields = new[] { coordinator.GetField("Selected", F), coordinator.GetField("Active", F),
            coordinator.GetField("InRun", F), coordinator.GetField("ResumePrepared", F),
            coordinator.GetField("departureAscent", F), coordinator.GetField("Notice", F) };
        var saved = new object[fields.Length];
        for (int i = 0; i < fields.Length; i++) saved[i] = fields[i].GetValue(null);
        var patches = new Harmony("dda.continued.client-rules-fixture");
        int checks = 0;
        try
        {
            room = new Room("synthetic-continued-room", new RoomOptions());
            player = (Player)Activator.CreateInstance(typeof(Player), F, null, new object[] { "fixture-client", 2, true }, null);
            Patch(AccessTools.PropertyGetter(typeof(PhotonNetwork), "InRoom"), nameof(InRoom));
            Patch(AccessTools.PropertyGetter(typeof(PhotonNetwork), "CurrentRoom"), nameof(CurrentRoom));
            Patch(AccessTools.PropertyGetter(typeof(PhotonNetwork), "LocalPlayer"), nameof(LocalPlayer));
            Patch(AccessTools.PropertyGetter(coordinator, "IsHost"), nameof(IsHost));
            Patch(AccessTools.PropertyGetter(coordinator, "InMenu"), nameof(InMenu));
            Patch(AccessTools.PropertyGetter(AccessTools.TypeByName("Ascents"), "currentAscent"), nameof(BaseAscent));
            var snapshotType = mod.GetType("dda.DifficultySnapshot");
            var official = snapshotType.GetField("Official", F).GetValue(null);
            var extended = Activator.CreateInstance(snapshotType, 20, 0, true, true);
            string encoded = (string)snapshotType.GetMethod("Encode").Invoke(extended, null);
            var receive = coordinator.GetMethod("Receive", F);
            var enabled = mod.GetType("dda.Rules").GetMethod("Enabled", F);
            menu = false; host = false;
            fields[0].SetValue(null, official); fields[1].SetValue(null, official);
            fields[2].SetValue(null, true); fields[3].SetValue(null, false);
            fields[4].SetValue(null, null); baseAscent = 8;
            // Departure has already set InRun, but authoritative rules arrive later.
            room.CustomProperties["dda.continued.rules"] = encoded;
            room.CustomProperties["dda.continued.running"] = true;
            receive.Invoke(null, new object[] { false });
            Assert((bool)enabled.Invoke(null, new object[] { 13 }), "late host snapshot must enable client fall drowsiness despite InRun already true");
            Assert((bool)enabled.Invoke(null, new object[] { 20 }), "late host snapshot must enable client weather");
            Assert(Equals(player.CustomProperties["dda.continued.ack"], encoded), "selection is acknowledged");
            Assert(Equals(player.CustomProperties["dda.continued.active"], encoded), "acknowledgement also reports actually active rules");
            var timer = mod.GetType("dda.allSun").GetField("timeTillNext", F);
            var oldTimer = timer.GetValue(null); timer.SetValue(null, 123f);
            for (int i = 0; i < 10; i++) receive.Invoke(null, new object[] { false });
            Assert((bool)enabled.Invoke(null, new object[] { 20 }), "duplicate callbacks preserve active rules");
            Assert((float)timer.GetValue(null) == 123f, "duplicate rules never restart weather clocks");
            timer.SetValue(null, oldTimer);
            checks += ClientFallRegression.Run(mod);

            // Airport departure: a stale running=false echo must not erase the RPC selection.
            coordinator.GetMethod("ReturnToAirport", F).Invoke(null, null);
            menu = true; room.CustomProperties["dda.continued.running"] = false;
            receive.Invoke(null, new object[] { false });
            Assert(!(bool)enabled.Invoke(null, new object[] { 20 }), "airport selection alone has no live penalties");
            coordinator.GetMethod("ReceiveDeparture", F).Invoke(null, new object[] { 8 });
            receive.Invoke(null, new object[] { false });
            Assert((bool)enabled.Invoke(null, new object[] { 20 }), "late airport property echo cannot clear departure rules");
            room.CustomProperties["dda.continued.running"] = true; menu = false;
            receive.Invoke(null, new object[] { false });
            Assert((bool)enabled.Invoke(null, new object[] { 20 }), "new game keeps the agreed tier after scene load");

            // Resume/late join without a kiosk RPC: reconcile a provisional official run.
            foreach (bool provisional in new[] { false, true })
            {
                coordinator.GetMethod("ReturnToAirport", F).Invoke(null, null);
                fields[2].SetValue(null, provisional);
                receive.Invoke(null, new object[] { false });
                Assert((bool)enabled.Invoke(null, new object[] { 13 }), "resume and late join adopt host rules, with or without provisional Begin");
            }
            host = true; fields[1].SetValue(null, official);
            receive.Invoke(null, new object[] { false });
            Assert(!(bool)enabled.Invoke(null, new object[] { 20 }), "ordinary receive cannot overwrite host-owned rules");
            receive.Invoke(null, new object[] { true });
            Assert((bool)enabled.Invoke(null, new object[] { 20 }), "promoted host adopts the room snapshot before publishing");
            host = false;
            coordinator.GetMethod("ReturnToAirport", F).Invoke(null, null);
            menu = true; baseAscent = 0;
            receive.Invoke(null, new object[] { false });
            Assert(!(bool)enabled.Invoke(null, new object[] { 20 }), "late joiner in menu waits before becoming host");
            host = true;
            var callbackRoot = new GameObject("Host handoff fixture"); callbackRoot.SetActive(false);
            try
            {
                var callbacks = callbackRoot.AddComponent(mod.GetType("dda.ContinuedNetwork"));
                callbacks.GetType().GetMethod("OnMasterClientSwitched", F).Invoke(callbacks, new object[] { player });
            }
            finally { UnityEngine.Object.DestroyImmediate(callbackRoot); }
            Assert((bool)enabled.Invoke(null, new object[] { 20 }), "host migration during loading preserves running room rules before the native ascent arrives");
            Assert(Equals(room.CustomProperties["dda.continued.running"], true), "loading successor does not overwrite active room with running=false");
            host = false; menu = false; baseAscent = 8;
            coordinator.GetMethod("LeaveRoom", F).Invoke(null, null);
            Assert(!(bool)enabled.Invoke(null, new object[] { 20 }), "leaving room clears active rules");
            room.CustomProperties.Clear();
            receive.Invoke(null, new object[] { false });
            Assert(!(bool)enabled.Invoke(null, new object[] { 20 }), "room without Continued metadata cannot inherit last run");
            room.CustomProperties["dda.continued.rules"] = encoded;
            room.CustomProperties["dda.continued.running"] = true;
            baseAscent = 7;
            receive.Invoke(null, new object[] { false });
            Assert(!(bool)enabled.Invoke(null, new object[] { 20 }), "extended rules never attach to an official non-8 base");
            room.CustomProperties["dda.continued.rules"] = "broken"; baseAscent = 8;
            receive.Invoke(null, new object[] { false });
            Assert(!(bool)enabled.Invoke(null, new object[] { 20 }), "malformed payload cannot activate effects");
            var forced = Activator.CreateInstance(snapshotType, 0, 1 << 4, true, true);
            room.CustomProperties["dda.continued.rules"] = snapshotType.GetMethod("Encode").Invoke(forced, null);
            baseAscent = 7; receive.Invoke(null, new object[] { false });
            Assert((bool)enabled.Invoke(null, new object[] { 13 }) && !(bool)enabled.Invoke(null, new object[] { 20 }), "forced tier options remain valid on official ascents");
            room.CustomProperties["dda.continued.rules"] = snapshotType.GetMethod("Encode").Invoke(official, null);
            receive.Invoke(null, new object[] { false });
            Assert(!(bool)enabled.Invoke(null, new object[] { 13 }), "official next run does not keep client penalties");
            return checks;
        }
        finally
        {
            patches.UnpatchSelf();
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, saved[i]);
            room = null; player = null;
        }
        void Patch(MethodInfo target, string name) => patches.Patch(target, prefix: new HarmonyMethod(typeof(ClientRulesRegression), name));
        void Assert(bool pass, string reason) { if (!pass) throw new Exception(reason); checks++; }
    }
    private static bool InRoom(ref bool __result) { __result = true; return false; }
    private static bool InMenu(ref bool __result) { __result = menu; return false; }
    private static bool IsHost(ref bool __result) { __result = host; return false; }
    private static bool CurrentRoom(ref Room __result) { __result = room; return false; }
    private static bool LocalPlayer(ref Player __result) { __result = player; return false; }
    private static bool BaseAscent(ref int __result) { __result = baseAscent; return false; }
}
