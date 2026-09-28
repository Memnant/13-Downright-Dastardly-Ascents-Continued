using System;
using System.Collections.Generic;
using HarmonyLib;
using Peak;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace dda;

internal static class SummitHonor
{
    internal const string RoomKey = "dda.continued.summitHonor.v1";
    private static readonly HashSet<string> issued = new();
    internal static bool Enabled => Plugin.Ready && RunCoordinator.InRun && Rules.Snapshot.GrantsSummitHonor;
    internal static string Identity(ScoutStatue statue) => statue.gameObject.scene.name + ":" + statue.GetComponent<PhotonView>().ViewID;
    internal static bool WasIssued(string identity) => issued.Contains(identity) ||
        (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties[RoomKey] is string[] previous &&
         Array.IndexOf(previous, identity) >= 0);

    internal static void Record(string identity)
    {
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties[RoomKey] is string[] previous)
            foreach (string entry in previous) issued.Add(entry);
        issued.Add(identity);
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
        var entries = new string[issued.Count];
        issued.CopyTo(entries);
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [RoomKey] = entries });
    }

    internal static void Reset() => issued.Clear();
    internal static void ClearRoom()
    {
        Reset();
        if (PhotonNetwork.InRoom && RunCoordinator.IsHost)
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [RoomKey] = null });
    }
}

[HarmonyPatch(typeof(ScoutStatue), "SpawnGem_Master")]
internal static class DirectSummitHonor
{
    private static bool Prefix(ScoutStatue __instance)
    {
        if (!SummitHonor.Enabled) return true;
        if (!PhotonNetwork.IsMasterClient || __instance.spawnedGem_master != null) return false;
        string identity = SummitHonor.Identity(__instance);
        // Native Update runs once again on a newly promoted host. Preserve the issued item,
        // including one already taken from the statue, instead of creating a second copy.
        if (SummitHonor.WasIssued(identity)) return false;
        if (__instance.scoutsHonorPrefab == null) return true;
        // Room ownership keeps the item when the creating host leaves. Pickup/use still
        // run the original Item code; its prefab, position and kinematic setup are native.
        var item = PhotonNetwork.InstantiateRoomObject("0_Items/" + __instance.scoutsHonorPrefab.name,
            __instance.gemSpot.position, __instance.gemSpot.rotation, 0);
        if (item == null) return false;
        __instance.spawnedGem_master = item;
        try { item.GetComponent<Item>().SetKinematicNetworked(true); }
        finally { SummitHonor.Record(identity); }
        return false;
    }
}

[HarmonyPatch(typeof(ScoutStatue), "SpawnScoutsHonor")]
internal static class SkipRedundantSummitConversion
{
    private static bool Prefix() => !SummitHonor.Enabled;
}

[HarmonyPatch(typeof(ScoutStatue), "IsConstantlyInteractable")]
internal static class SkipSummitAmuletPrompt
{
    private static void Postfix(ref bool __result) { if (SummitHonor.Enabled) __result = false; }
}

[HarmonyPatch(typeof(ScoutStatue), "Interact_CastFinished")]
internal static class KeepUnneededSummitAmulets
{
    private static bool Prefix() => !SummitHonor.Enabled;
}

[HarmonyPatch(typeof(ScoutStatue), "RPC_InsertAmulet")]
internal static class IgnoreUnneededSummitAmulets
{
    private static bool Prefix(int amuletType) => !SummitHonor.Enabled || amuletType < 0;
}
