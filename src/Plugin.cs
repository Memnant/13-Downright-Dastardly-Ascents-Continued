using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace dda;

[BepInPlugin(Guid, "13 Downright Dastardly Ascents Continued", Version)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = "13dastardlyascents";
    public const string Version = "1.5.20";
    public const string SupportedGame = "2.6.a";
    internal static ManualLogSource Log;
    internal static bool Ready;
    internal static readonly ConfigEntry<bool>[] ForcedLevels = new ConfigEntry<bool>[12];
    private static ConfigEntry<bool> nerfedRevives;
    private static ConfigEntry<bool> cursePunishment;
    private static ConfigEntry<bool> freeSummitHonor;
    private Harmony harmony;

    private void Awake()
    {
        Log = Logger;
        try
        {
            string versionFile = Path.Combine(Paths.GameRootPath, "version.txt");
            string gameVersion = File.ReadAllLines(versionFile)[0].Trim();
            if (gameVersion != SupportedGame)
                throw new NotSupportedException($"Continued {Version} targets PEAK {SupportedGame}; found {gameVersion}. Install the matching Continued release; game updates can change status and save interfaces.");
            for (int level = 9; level <= 20; level++)
            {
                ForcedLevels[level - 9] = Config.Bind("Ascents", "Ascent " + level, false,
                    "在任意官方天阶强制启用此效果；默认关闭。续作模式会自动逐阶累加。" + EffectDescriptions.Get(level));
            }
            nerfedRevives = Config.Bind("Settings For Big Lobbies", "Nerfed Revives", true,
                "天阶 12：保留清除状态后的 25% 残留，并削弱原本不附加状态的复活。官方复活流程继续执行。");
            cursePunishment = Config.Bind("Settings For Big Lobbies", "Curse Punishment for Ascent 10", true,
                "天阶 10：队友死亡时，仍存活的玩家承受原模组的团队诅咒惩罚。");
            freeSummitHonor = Config.Bind("Continued Host Options", "Free Summit Honor", false,
                "续作天阶 9–20：顶峰雕像直接提供童军的荣耀，免收集四颗宝石；后续原版通关流程保留。房主在机场选择，开局固定并随营火保存。官方模式不生效。");
            harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(Plugin).Assembly);
            Ready = true;
            gameObject.AddComponent<ContinuedNetwork>();
            SceneManager.sceneLoaded += OnSceneLoaded;
            Log.LogInfo($"Continued {Version} loaded for PEAK {gameVersion}. Official 7/8 retained. Fog status starts at 14; movement and swamp sleep fog at 18. Volcano tint restored; native fog boundary and network flow retained.");
        }
        catch (Exception error)
        {
            Ready = false;
            harmony?.UnpatchSelf();
            Log.LogError("Continued disabled; its patches were removed. " + error);
        }
    }

    internal static DifficultySnapshot Selection(int level)
    {
        int mask = 0;
        for (int i = 0; i < ForcedLevels.Length; i++)
            if (ForcedLevels[i]?.Value == true) mask |= 1 << i;
        return new DifficultySnapshot(level, mask, nerfedRevives?.Value ?? true, cursePunishment?.Value ?? true,
            freeSummitHonor?.Value ?? false);
    }

    internal static void SetFreeSummitHonor(bool enabled)
    {
        if (!RunCoordinator.IsHost || RunCoordinator.InRun || freeSummitHonor == null) return;
        freeSummitHonor.Value = enabled;
        RunCoordinator.Select(RunCoordinator.Selected.Ascent);
    }

    internal static void AdoptHostOption()
    {
        if (RunCoordinator.IsHost && freeSummitHonor != null)
            freeSummitHonor.Value = RunCoordinator.Selected.FreeSummitHonor;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single && (scene.name == "Airport" || scene.name == "MainMenu"))
            RunCoordinator.ReturnToAirport();
        else if (mode == LoadSceneMode.Single && Photon.Pun.PhotonNetwork.InRoom)
            RunCoordinator.Receive();
    }

    private void OnDestroy()
    {
        Ready = false;
        OpeningProtection.Reset();
        WeatherGrace.Reset();
        TornadoRecovery.Reset();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EveryMapSnow.Reset();
        NativeSnowAssets.Release();
        EffectState.Reset();
        harmony?.UnpatchSelf();
    }
}

internal static class EffectDescriptions
{
    private static readonly string[] Text = {
        "环境危害更加致命；水母和海岸毒刺每次 40 点毒，不叠加天阶 17。", "规则 0 更严格；队友距离过远会被追击，死亡可连带诅咒。",
        "打开行李箱会受伤，稀有箱子的伤害更高。", "食物、治疗、工具及部分复活效果削弱；滑翔翼持续耗体力 ×1.5。",
        "受到坠落伤害会迅速积累困倦。", "强化风暴、龙卷风与火山岩浆；追赶雾改为毒雾，基础状态增长 ×3（火山炎热、骷髅伤势）。",
        "城塞倦霾等待 30 秒，上升速度为原版 ×2.5（全程 480 秒）；天底石化雾按原版触发后以 ×2.5 速度上升（全程 248 秒）；熔炉 30/700 秒。", "状态额外惩罚；夜间困倦不自然恢复，白天每秒恢复 2 点（满条按 100 点计）。",
        "寒冷、困倦增加 30%，炎热增加 50%，中毒增加 75%；20 阶雾沼困倦不加成，水母和海岸毒刺固定 40 点毒。", "追赶雾速度 ×2，等待超过 20 秒启动，覆盖火山与雾沼；雾沼被追上额外增加 2 倍环境困倦。火山/熔炉炎热、雾沼/城塞困倦每秒恢复约 0.33 点，20 阶雾沼约 0.67 点；夜间不恢复。",
        "日照在更多区域施加状态；保留原作者在雪山施加寒冷的规则。", "全图雪暴与额外龙卷风，最终关除外。雾沼追赶雾额外加 2 倍困倦，白天恢复 1/3；雪暴停歇 60–120 秒。开局/续玩缓冲 30 秒，新局无敌 30 秒。"
    };
    internal static string Get(int level) => level >= 9 && level <= 20 ? Text[level - 9] : "官方规则";
}
