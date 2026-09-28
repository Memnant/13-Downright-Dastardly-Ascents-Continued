using System;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using NetworkPlayer = Photon.Realtime.Player;

namespace dda;

internal static class Rules
{
    internal static DifficultySnapshot Snapshot => RunCoordinator.InRun ? RunCoordinator.Active : DifficultySnapshot.Official;
    internal static int Level => Snapshot.Ascent;
    internal static bool Enabled(int level) => Plugin.Ready && RunCoordinator.InRun && Snapshot.Enabled(level);
    internal static bool Forced(int level) => level >= 9 && level <= 20 && RunCoordinator.InRun &&
        (Snapshot.ForceMask & (1 << (level - 9))) != 0;
    internal static bool Owns(Character character) => character != null && character.IsLocal;
}

internal static class RunCoordinator
{
    internal const string VersionKey = "dda.continued.version";
    internal const string RulesKey = "dda.continued.rules";
    internal const string AckKey = "dda.continued.ack";
    internal const string RunningKey = "dda.continued.running";
    internal static DifficultySnapshot Selected = DifficultySnapshot.Official;
    internal static DifficultySnapshot Active = DifficultySnapshot.Official;
    internal static bool InRun;
    internal static bool ResumePrepared;
    internal static string Notice = "";
    internal static bool IsHost => !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;
    private static bool InMenu => SceneManager.GetActiveScene().name == "Airport" || SceneManager.GetActiveScene().name == "MainMenu";

    internal static void Select(int level)
    {
        if (!IsHost || InRun) return;
        Selected = Plugin.Selection(level);
        Notice = "";
        Publish(false);
    }

    internal static void Advertise()
    {
        if (!PhotonNetwork.InRoom) return;
        var player = PhotonNetwork.LocalPlayer;
        string rules = Selected.Encode();
        if ((string)player.CustomProperties[VersionKey] == Plugin.Version &&
            (string)player.CustomProperties[AckKey] == rules) return;
        player.SetCustomProperties(new Hashtable { [VersionKey] = Plugin.Version, [AckKey] = rules });
    }

    internal static void Publish(bool running)
    {
        if (!PhotonNetwork.InRoom || !IsHost) return;
        string encoded = (running ? Active : Selected).Encode();
        var properties = PhotonNetwork.CurrentRoom.CustomProperties;
        if ((string)properties[RulesKey] != encoded || !Equals(properties[RunningKey], running))
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [RulesKey] = encoded, [RunningKey] = running });
        Advertise();
    }

    internal static void Receive(bool acceptAsHost = false)
    {
        if (!PhotonNetwork.InRoom) return;
        var properties = PhotonNetwork.CurrentRoom.CustomProperties;
        if (IsHost && !acceptAsHost) { Advertise(); return; }
        if (DifficultySnapshot.TryDecode(properties[RulesKey] as string, out var value))
        {
            Selected = value;
            // Room snapshot is immutable for the duration of a run, including host migration.
            if (Equals(properties[RunningKey], true) && !InRun && !InMenu) Begin(value);
            else if (Equals(properties[RunningKey], false) && InMenu && !ResumePrepared)
            { InRun = false; Active = DifficultySnapshot.Official; }
        }
        else if (!IsHost && !InRun) Selected = DifficultySnapshot.Official;
        Advertise();
    }

    internal static bool AllReady(out string reason)
    {
        reason = "";
        if (!Selected.HasEffects || !PhotonNetwork.InRoom || PhotonNetwork.OfflineMode) return true;
        string required = Selected.Encode();
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (ReadinessPolicy.Check(Selected, Plugin.Version, player.CustomProperties[VersionKey] as string,
                player.CustomProperties[AckKey] as string) == PeerReadiness.VersionMismatch)
            { reason = "还有玩家未安装相同版本的 Continued " + Plugin.Version + "。"; return false; }
            if (ReadinessPolicy.Check(Selected, Plugin.Version, player.CustomProperties[VersionKey] as string,
                player.CustomProperties[AckKey] as string) == PeerReadiness.RulesPending)
            { reason = "正在等待所有玩家同步难度配置，请稍后再开始。"; return false; }
        }
        return true;
    }

    internal static bool PrepareDeparture(ref int ascent, ref byte[] serializedSettings)
    {
        if (!IsHost) return true;
        bool freshDeparture = !InRun;
        if (!InRun) Selected = Plugin.Selection(Selected.Ascent);
        Publish(false);
        if (!AllReady(out var reason)) { Notice = reason; Plugin.Log.LogWarning(reason); return false; }
        if (Selected.IsExtended)
        {
            ascent = 8;
            RunSettings.IsCustomRun = false;
            serializedSettings = RunSettings.GetSerializedRunSettings();
        }
        Begin(Selected);
        if (freshDeparture) { SummitHonor.ClearRoom(); OpeningProtection.Arm(); WeatherGrace.Arm(); }
        Publish(true);
        return true;
    }

    internal static void Begin(DifficultySnapshot snapshot, bool fresh = false)
    {
        bool initialize = !InRun || fresh;
        Active = snapshot;
        Selected = snapshot;
        InRun = true;
        Notice = "";
        if (initialize) ResetTimers();
    }

    internal static void ReceiveDeparture(int ascent)
    {
        Receive();
        var snapshot = Selected;
        if (snapshot.IsExtended && ascent != 8)
        {
            Notice = "本局不是官方天阶 8 基础，已停止应用续作效果。";
            snapshot = DifficultySnapshot.Official;
        }
        Begin(snapshot);
    }

    internal static void ReturnToAirport()
    {
        EveryMapSnow.Reset();
        InRun = false;
        ResumePrepared = false;
        Active = DifficultySnapshot.Official;
        EffectState.Reset();
        ResetTimers();
        if (IsHost)
        {
            Selected = Plugin.Selection(0); Publish(false);
            if (PhotonNetwork.InRoom) PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable {
                [TideSync.RoomKey] = null, [RuleZeroController.RoomKey] = null, [SnowWeatherSync.RoomKey] = null,
                [SummitHonor.RoomKey] = null, [OpeningProtection.RoomKey] = null, [WeatherGrace.RoomKey] = null });
        }
    }

    internal static void LeaveRoom()
    {
        if (ResumePrepared) return;
        ReturnToAirport();
        Selected = DifficultySnapshot.Official;
    }

    private static void ResetTimers()
    {
        allSun.timeTillNext = 5f;
        TideSync.Reset();
        RuleZeroController.Reset();
        SnowWeatherSync.Reset();
        SummitHonor.Reset();
        OpeningProtection.Reset();
        WeatherGrace.Reset();
        TornadoRecovery.Reset();
    }
}

internal sealed class ContinuedNetwork : MonoBehaviourPunCallbacks
{
    private float nextCheck;
    public override void OnJoinedRoom()
    {
        if (RunCoordinator.ResumePrepared && RunCoordinator.IsHost) RunCoordinator.Publish(true);
        else if (PhotonNetwork.CurrentRoom.CustomProperties[RunCoordinator.RulesKey] != null) RunCoordinator.Receive(true);
        else if (RunCoordinator.IsHost) RunCoordinator.Select(0);
        WeatherGrace.JoinedRoom();
        RunCoordinator.Advertise();
    }
    public override void OnLeftRoom() => RunCoordinator.LeaveRoom();
    public override void OnRoomPropertiesUpdate(Hashtable changed)
    { RunCoordinator.Receive(); TideSync.Receive(); RuleZeroController.Receive(); SnowWeatherSync.Receive(); OpeningProtection.Receive(); WeatherGrace.Receive(); }
    public override void OnMasterClientSwitched(NetworkPlayer next)
    {
        RunCoordinator.Receive(true);
        TideSync.Receive(true);
        RuleZeroController.Receive(true);
        SnowWeatherSync.Receive(true);
        OpeningProtection.Receive(true);
        WeatherGrace.Receive(true);
        if (RunCoordinator.IsHost) { Plugin.AdoptHostOption(); RunCoordinator.Publish(RunCoordinator.InRun); }
    }
    public override void OnPlayerEnteredRoom(NetworkPlayer player) => RunCoordinator.Advertise();
    private void Update()
    {
        TideSync.Tick();
        RuleZeroController.Tick();
        OpeningProtection.Tick();
        WeatherGrace.Tick();
        TornadoRecovery.Tick();
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;
        if (PhotonNetwork.InRoom) { RunCoordinator.Receive(); RunCoordinator.Advertise(); }
        EveryMapSnow.Tick();
    }
}
