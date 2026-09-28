using System;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace dda;

internal static class TideSync
{
    internal const string RoomKey = "dda.continued.tide.v1";
    private static TideAnchor anchor;
    private static string received;
    private static Guid pendingRun;
    private static double pendingPhase;
    private static Guid cachedRun;
    private static string cachedRunKey;
    internal static double Clock => PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;
    internal static Guid RunId => RunManager.Instance == null ? Guid.Empty : RunManager.Instance.RunId;
    internal static string RunKey
    {
        get
        {
            var id = RunId;
            if (cachedRunKey == null || cachedRun != id) { cachedRun = id; cachedRunKey = id.ToString("N"); }
            return cachedRunKey;
        }
    }
    internal static void Reset()
    { anchor = null; received = null; pendingRun = Guid.Empty; pendingPhase = 0; cachedRunKey = null; }
    internal static void PrepareResume(Guid id, double phase)
    { anchor = null; received = null; pendingRun = id; pendingPhase = phase; }
    internal static void Receive(bool force = false)
    {
        if (!PhotonNetwork.InRoom || RunCoordinator.ResumePrepared || (RunCoordinator.IsHost && !force)) return;
        string encoded = PhotonNetwork.CurrentRoom.CustomProperties[RoomKey] as string;
        if (!force && encoded == received) return;
        received = encoded;
        if (string.IsNullOrEmpty(encoded)) { anchor = null; return; }
        try
        {
            var next = WireJson.Decode<TideAnchor>(encoded);
            if (next != null && next.Valid) anchor = next;
        }
        catch (Exception error) { Plugin.Log.LogWarning("Ignored invalid tide snapshot: " + error.Message); }
    }
    internal static void Tick()
    {
        if (!RunCoordinator.InRun || !Rules.Enabled(14)) return;
        if (!RunCoordinator.IsHost) { Receive(); return; }
        if (RunCoordinator.ResumePrepared || RunId == Guid.Empty) return;
        string id = RunKey;
        bool running = !LoadingScreenHandler.loading && Time.timeScale > 0f && MapHandler.Exists &&
            Singleton<MapHandler>.Instance.GetCurrentBiome() == Biome.BiomeType.Volcano;
        if (anchor == null || anchor.runId != id)
        {
            anchor = new TideAnchor { runId = id, phase = pendingRun == RunId ? pendingPhase : 0,
                clock = Clock, running = running };
            pendingRun = Guid.Empty;
            Publish();
        }
        else if (anchor.running != running)
        {
            anchor = new TideAnchor { runId = id, phase = anchor.At(Clock), clock = Clock, running = running };
            Publish();
        }
    }
    private static void Publish()
    {
        if (!PhotonNetwork.InRoom) return;
        received = WireJson.Encode(anchor);
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [RoomKey] = received });
    }
    internal static bool TryPhase(Guid id, out double phase)
    {
        Tick();
        phase = 0;
        if (anchor == null || id != RunId || anchor.runId != RunKey) return false;
        phase = anchor.At(Clock);
        return true;
    }
    // Resolve the same height before Heat (Update) and contact damage (FixedUpdate).
    internal static bool Apply(Lava lava)
    {
        if (!Rules.Enabled(14) || lava.name != "River" || !MapHandler.Exists ||
            Singleton<MapHandler>.Instance.GetCurrentBiome() != Biome.BiomeType.Volcano) return true;
        var animator = lava.GetComponent<Animator>();
        if (animator == null) return true;
        if (animator.enabled) EffectState.Set(animator, "enabled", false);
        if (!TryPhase(RunId, out double phase)) return false;
        Vector3 pos = lava.transform.position;
        pos.y = (float)VolcanoTide.Height(phase);
        lava.transform.position = pos;
        return true;
    }
}
