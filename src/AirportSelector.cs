using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace dda;

[HarmonyPatch(typeof(BoardingPass), "OnOpen")]
internal static class CreateContinuedSelector
{
    private static void Postfix(BoardingPass __instance)
    {
        if (__instance.GetComponent<AirportSelector>() == null) __instance.gameObject.AddComponent<AirportSelector>();
    }
}

[HarmonyPatch(typeof(BoardingPass), "UpdateAscent")]
internal static class ContinuedBoardingDescription
{
    private static void Prefix(BoardingPass __instance)
    {
        if (RunCoordinator.Selected.IsExtended && !RunSettings.IsCustomRun) __instance.ascentIndex = 8;
    }
    private static void Postfix(BoardingPass __instance)
    {
        if (!RunCoordinator.Selected.IsExtended || RunSettings.IsCustomRun) return;
        int level = RunCoordinator.Selected.Ascent;
        __instance.ascentTitle.text = "续作天阶 " + level;
        __instance.ascentDesc.text = EffectDescriptions.Get(level) + "\n\n包含官方天阶 8 与续作天阶 9–" + level + " 的累加效果。" +
            (RunCoordinator.Selected.GrantsSummitHonor ? "\n免宝石已开启：登顶领取童军的荣耀，继续后续通关流程。" : "");
        __instance.reward.SetActive(false);
        __instance.incrementAscentButton.interactable = false;
        __instance.decrementAscentButton.interactable = false;
    }
}

[HarmonyPatch(typeof(BoardingPass), "ToggleCustom")]
internal static class SeparateCustomRuns
{
    private static void Prefix(BoardingPass __instance)
    {
        if (RunCoordinator.Selected.IsExtended)
        {
            RunCoordinator.Select(0);
            __instance.ascentIndex = 0;
        }
    }
}

internal sealed class AirportSelector : MonoBehaviour
{
    private BoardingPass pass;
    private GameObject canvasObject;
    private TMP_Text selectionText, detailText, statusText, toggleText, honorText;
    private Button toggle, minus, plus, honorToggle;
    private int originalSelection;
    private int lastDisplayed = -1;
    private bool lastHonor;
    private bool blockedStart;
    private bool savedStartInteractable;

    private void Start()
    {
        pass = GetComponent<BoardingPass>();
        originalSelection = pass.ascentIndex == 8 && RunCoordinator.Selected.IsExtended ? 0 : pass.ascentIndex;
        canvasObject = new GameObject("ContinuedAscentCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var panel = new GameObject("ContinuedPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1, 0.5f);
        rect.pivot = new Vector2(1, 0.5f);
        rect.anchoredPosition = new Vector2(-28, 0);
        rect.sizeDelta = new Vector2(330, 565);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.11f, 0.12f, 0.97f);
        MakeText(panel.transform, "天阶延续 · Continued", 18, -20, 294, 34, 25);
        toggle = MakeButton(panel.transform, "启用续作", 18, -70, 294, 45, out toggleText);
        toggle.onClick.AddListener(Toggle);
        minus = MakeButton(panel.transform, "−", 18, -132, 55, 45, out _);
        plus = MakeButton(panel.transform, "+", 257, -132, 55, 45, out _);
        minus.onClick.AddListener(() => Change(-1));
        plus.onClick.AddListener(() => Change(1));
        selectionText = MakeText(panel.transform, "官方模式", 84, -134, 163, 42, 23);
        selectionText.alignment = TextAlignmentOptions.Center;
        detailText = MakeText(panel.transform, "", 18, -194, 294, 150, 19);
        honorToggle = MakeButton(panel.transform, "免宝石通关：关", 18, -357, 294, 45, out honorText);
        honorToggle.onClick.AddListener(() => Plugin.SetFreeSummitHonor(!RunCoordinator.Selected.FreeSummitHonor));
        MakeText(panel.transform, "开启后在顶峰领取荣耀，仍须完成后续流程。", 18, -413, 294, 49, 16);
        statusText = MakeText(panel.transform, "", 18, -478, 294, 70, 17);
        statusText.color = new Color(0.93f, 0.80f, 0.47f);
    }

    private void Update()
    {
        if (canvasObject == null || pass == null) return;
        canvasObject.SetActive(pass.isOpen);
        if (!pass.isOpen) return;
        bool host = RunCoordinator.IsHost && !RunCoordinator.InRun && !LoadingScreenHandler.loading;
        int level = RunCoordinator.Selected.Ascent;
        bool extended = level >= 9;
        toggle.interactable = host;
        minus.interactable = host && level > 9;
        plus.interactable = host && extended && level < 20;
        honorToggle.interactable = host && extended;
        honorText.text = "免宝石通关：" + (RunCoordinator.Selected.FreeSummitHonor ? "开" : "关");
        toggleText.text = extended ? "切回官方模式" : "启用续作天阶 9–20";
        selectionText.text = extended ? "天阶 " + level : "官方模式";
        detailText.text = extended ? EffectDescriptions.Get(level) + "\n\n保留官方 7、8；18 阶追赶雾覆盖火山、雾沼。" : "官方难度按原有进度解锁。\n续作 9–20 全部可选，独立于官方天阶进度。";
        bool ready = RunCoordinator.AllReady(out string reason);
        statusText.text = !string.IsNullOrEmpty(RunCoordinator.Notice) ? RunCoordinator.Notice :
            (!host ? "难度由房主选择。\n" : "") + (ready ? "使用相同版本即可开始。" : reason);
        if (!ready && RunCoordinator.Selected.HasEffects)
        {
            if (!blockedStart) savedStartInteractable = pass.startGameButton.interactable;
            blockedStart = true;
            pass.startGameButton.interactable = false;
        }
        else if (blockedStart)
        {
            pass.startGameButton.interactable = savedStartInteractable;
            blockedStart = false;
        }
        if (level != lastDisplayed || lastHonor != RunCoordinator.Selected.FreeSummitHonor)
        {
            lastDisplayed = level;
            lastHonor = RunCoordinator.Selected.FreeSummitHonor;
            if (extended) { RunSettings.IsCustomRun = false; pass.ascentIndex = 8; }
            else pass.ascentIndex = Mathf.Clamp(originalSelection, -1, 8);
            pass.UpdateAscent();
        }
    }

    private void Toggle()
    {
        if (RunCoordinator.Selected.IsExtended) RunCoordinator.Select(0);
        else { originalSelection = pass.ascentIndex; RunSettings.IsCustomRun = false; RunCoordinator.Select(9); }
    }
    private void Change(int delta) => RunCoordinator.Select(Mathf.Clamp(RunCoordinator.Selected.Ascent + delta, 9, 20));
    private void OnDestroy() { if (canvasObject != null) Destroy(canvasObject); }

    private TMP_Text MakeText(Transform parent, string text, float x, float y, float width, float height, float fontSize)
    {
        var obj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        Position(obj.GetComponent<RectTransform>(), x, y, width, height);
        var label = obj.GetComponent<TextMeshProUGUI>();
        label.font = pass.ascentDesc.font;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.text = text;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }
    private Button MakeButton(Transform parent, string text, float x, float y, float width, float height, out TMP_Text label)
    {
        var obj = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        Position(obj.GetComponent<RectTransform>(), x, y, width, height);
        obj.GetComponent<Image>().color = new Color(0.20f, 0.35f, 0.32f, 1f);
        var button = obj.GetComponent<Button>();
        button.targetGraphic = obj.GetComponent<Image>();
        label = MakeText(obj.transform, text, 2, -2, width - 4, height - 4, 20);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }
    private static void Position(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
