using System;
using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace dda;

internal static class RuleZeroController
{
    internal const string RoomKey = "dda.continued.scout.v1";
    private static ScoutmasterLease lease;
    private static Scoutmaster ownedScout;
    private static int ownedTarget;
    private static float ownedUntil;
    private static string received, appliedGeneration;
    private static int appliedSequence;
    private static readonly List<ScoutPosition> scouts = new(16);
    private static float nextCheck;
    private static bool applying;
    private static bool IsApplied => lease != null && appliedGeneration == lease.generation && appliedSequence == lease.sequence;
    private static void MarkApplied() { appliedGeneration = lease.generation; appliedSequence = lease.sequence; }
    internal static void Reset()
    {
        ReleaseLocal();
        lease = null; received = appliedGeneration = null; appliedSequence = 0; nextCheck = 0;
        scouts.Clear();
    }
    internal static void Receive(bool force = false)
    {
        if (!PhotonNetwork.InRoom || (RunCoordinator.IsHost && !force)) return;
        string encoded = PhotonNetwork.CurrentRoom.CustomProperties[RoomKey] as string;
        if (!force && received == encoded) return;
        received = encoded;
        if (string.IsNullOrEmpty(encoded)) { ReleaseLocal(); lease = null; return; }
        try
        {
            var next = WireJson.Decode<ScoutmasterLease>(encoded);
            if (next != null && next.Valid) lease = next;
        }
        catch (Exception error) { Plugin.Log.LogWarning("Ignored invalid scout lease: " + error.Message); }
    }
    private static bool OwnsTarget => ownedScout != null && ownedScout.currentTarget != null &&
        ownedScout.currentTarget.photonView.ViewID == ownedTarget && ownedScout.targetForcedUntil == ownedUntil;
    private static void ReleaseLocal()
    {
        if (OwnsTarget)
        {
            applying = true;
            try
            {
                ownedScout.targetForcedUntil = Time.time;
                ownedScout.RPCA_SetCurrentTarget(-1, 0f);
            }
            finally { applying = false; }
        }
        ownedScout = null;
    }
    private static void Apply()
    {
        if (lease == null || IsApplied || lease.runId != TideSync.RunKey) return;
        if (!lease.active) { ReleaseLocal(); MarkApplied(); return; }
        var scoutView = PhotonNetwork.GetPhotonView(lease.scoutId);
        var targetView = PhotonNetwork.GetPhotonView(lease.targetId);
        if (scoutView == null || targetView == null) return;
        var scout = scoutView.GetComponent<Scoutmaster>();
        var target = targetView.GetComponent<Character>();
        if (scout == null || target == null) return;
        // Keep 1.5.0's 30-second forced target window, not a 30-second pursuit cap.
        // A late join after that window receives an ordinary, unforced pursuit.
        float remaining = Math.Max(0f, 30f - (float)WireJson.Elapsed(TideSync.Clock, lease.clock));
        if (scout.currentTarget != null || scout.isThrowing)
        {
            // A client's preceding native clear RPC can arrive after these room properties.
            // Keep the lease pending there; the host can instead relinquish to a real takeover.
            if (RunCoordinator.IsHost) MarkApplied();
            return;
        }
        applying = true;
        try
        {
            scout.RPCA_SetCurrentTarget(lease.targetId, remaining);
            if (scout.currentTarget == target)
            { ownedScout = scout; ownedTarget = lease.targetId; ownedUntil = scout.targetForcedUntil; MarkApplied(); }
        }
        finally { applying = false; }
    }
    private static List<ScoutPosition> Scouts()
    {
        scouts.Clear();
        foreach (var character in Character.AllCharacters)
        {
            if (character == null || character.isBot || character.data.isScoutmaster ||
                !character.data.fullyConscious || character.photonView == null) continue;
            Vector3 pos = character.Center;
            scouts.Add(new ScoutPosition(character.photonView.ViewID, pos.x, pos.y, pos.z));
        }
        return scouts;
    }
    private static void Publish(bool active, Scoutmaster scout = null, int target = 0)
    {
        lease = new ScoutmasterLease {
            runId = TideSync.RunKey, generation = lease?.generation ?? Guid.NewGuid().ToString("N"),
            sequence = (lease?.sequence ?? 0) + 1, scoutId = scout != null ? scout.view.ViewID : lease.scoutId,
            targetId = active ? target : lease.targetId, active = active, clock = TideSync.Clock
        };
        received = WireJson.Encode(lease);
        if (PhotonNetwork.InRoom) PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [RoomKey] = received });
        Apply();
    }
    internal static void Tick()
    {
        if (!Rules.Enabled(10) || RunCoordinator.ResumePrepared || LoadingScreenHandler.loading || TideSync.RunId == Guid.Empty) return;
        Receive();
        Apply();
        if (!RunCoordinator.IsHost) return;
        if (lease != null && lease.runId != TideSync.RunKey) Reset();
        if (lease?.active == true)
        {
            if (ownedScout != null && ownedScout.isThrowing) return;
            if ((IsApplied && !OwnsTarget) || (ownedScout != null && ownedScout.preventSpawning) ||
                RuleZeroPolicy.ShouldRelease(Scouts(), lease.targetId))
            { Publish(false); nextCheck = Time.time + 5f; }
            return;
        }
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 5f;
        if (Scoutmaster.AllScoutmasters.Count >= 2 || !Scoutmaster.GetPrimaryScoutmaster(out var scout) ||
            scout.currentTarget != null || scout.isThrowing || scout.preventSpawning) return;
        int target = RuleZeroPolicy.PickTarget(Scouts());
        if (target > 0) Publish(true, scout, target);
    }
    internal static void BeforeNativeTarget(Scoutmaster scout, float forceForTime)
    {
        if (applying || scout != ownedScout || forceForTime <= 0f) return;
        if (OwnsTarget) scout.targetForcedUntil = Time.time;
        ownedScout = null; // Other forced summons take ownership.
    }
    internal static void AfterNativeTarget(Scoutmaster scout)
    {
        if (!applying && scout == ownedScout && !OwnsTarget) ownedScout = null;
    }
}

[HarmonyPatch(typeof(Scoutmaster), "RPCA_SetCurrentTarget")]
internal static class ContinuedScoutTarget
{
    private static void Prefix(Scoutmaster __instance, float forceForTime) => RuleZeroController.BeforeNativeTarget(__instance, forceForTime);
    private static void Postfix(Scoutmaster __instance) => RuleZeroController.AfterNativeTarget(__instance);
}
