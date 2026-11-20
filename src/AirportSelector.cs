using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace dda;

[HarmonyPatch(typeof(BoardingPass), "OnOpen")]
internal static class CreateContinuedSelector
{
    private static void Prefix(BoardingPass __instance)
    {
        if (!__instance.TryGetComponent<AirportSelector>(out _))
            __instance.gameObject.AddComponent<AirportSelector>();
    }
}

[HarmonyPatch(typeof(BoardingPass), "UpdateAscent")]
internal static class ContinuedBoardingDescription
{
    private static void Prefix()
    {
        if (RunCoordinator.Selected.IsExtended && RunSettings.IsCustomRun)
            RunSettings.IsCustomRun = false;
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(BoardingPass __instance)
    {
        if (__instance.TryGetComponent<AirportSelector>(out var selector)) selector.Refresh();
    }
}

[HarmonyPatch(typeof(BoardingPass), nameof(BoardingPass.IncrementAscent))]
internal static class NextBoardingAscent
{
    private static bool Prefix(BoardingPass __instance) => AirportSelector.Step(__instance, 1);
}

[HarmonyPatch(typeof(BoardingPass), nameof(BoardingPass.DecrementAscent))]
internal static class PreviousBoardingAscent
{
    private static bool Prefix(BoardingPass __instance) => AirportSelector.Step(__instance, -1);
}

[HarmonyPatch(typeof(BoardingPass), "ToggleCustom")]
internal static class SeparateCustomRuns
{
    private static bool Prefix()
    {
        if (!RunCoordinator.Selected.IsExtended) return true;
        if (!AirportSelector.CanEdit) return false;
        RunCoordinator.Select(0);
        return true;
    }
}

internal sealed class AirportSelector : MonoBehaviour
{
    private const float HonorRowFraction = 0.22f;
    private const float ButtonGap = 12f;
    private BoardingPass pass;
    private Button honorButton;
    private TMP_Text honorLabel;
    private Vector2 fullPosition, fullSizeDelta;
    private string displayedRules, displayedMessage;
    private bool displayedCanEdit, startBlocked, customBlocked;
    private bool startWasInteractable, customWasInteractable;
    private float nextCheck;

    internal static bool CanEdit => RunCoordinator.IsHost && !RunCoordinator.InRun && !LoadingScreenHandler.loading;
    private static int LastOfficial(BoardingPass ticket) =>
        Mathf.Clamp(ticket.maxUnlockedAscent, 0, DifficultySnapshot.FirstLevel - 1);

    internal static bool Step(BoardingPass ticket, int direction)
    {
        if (RunSettings.IsCustomRun || LoadingScreenHandler.loading || RunCoordinator.InRun) return false;
        var selected = RunCoordinator.Selected;
        int next;
        if (!selected.IsExtended)
        {
            if (direction < 0) return ticket.ascentIndex > -1;
            if (ticket.ascentIndex < LastOfficial(ticket)) return true;
            next = DifficultySnapshot.FirstLevel;
        }
        else
        {
            next = selected.Ascent + direction;
            if (next > DifficultySnapshot.LastLevel) return false;
            if (next < DifficultySnapshot.FirstLevel) next = 0;
        }

        if (!CanEdit) return false;
        // The game's title/reward arrays only accept official indices. The host
        // supplies base ascent 8 at departure, even for a new player's lobby.
        if (next == 0) ticket.ascentIndex = LastOfficial(ticket);
        RunCoordinator.Select(next);
        ticket.UpdateAscent();
        return false;
    }

    private void Awake()
    {
        pass = GetComponent<BoardingPass>();
        honorButton = Instantiate(pass.customOptionsButton, pass.startGameButton.transform.parent, false);
        honorButton.gameObject.name = "ContinuedFreeSummitHonor";
        honorButton.gameObject.SetActive(false);
        honorButton.onClick = new Button.ButtonClickedEvent();
        honorButton.onClick.AddListener(ToggleHonor);
        honorLabel = honorButton.GetComponentInChildren<TMP_Text>(true);
        if (honorLabel.TryGetComponent<LocalizedText>(out var nativeCaption)) nativeCaption.enabled = false;
        honorLabel.rectTransform.anchorMin = Vector2.zero;
        honorLabel.rectTransform.anchorMax = Vector2.one;
        honorLabel.rectTransform.offsetMin = new Vector2(8f, 2f);
        honorLabel.rectTransform.offsetMax = new Vector2(-8f, -2f);
        honorLabel.alignment = TextAlignmentOptions.Center;
        honorLabel.textWrappingMode = TextWrappingModes.NoWrap;
        honorLabel.enableAutoSizing = true;
        honorLabel.fontSizeMin = 16f;
        honorLabel.fontSizeMax = 28f;
        honorLabel.raycastTarget = false;
    }

    private void ToggleHonor()
    {
        if (!CanEdit || !RunCoordinator.Selected.IsExtended) return;
        Plugin.SetFreeSummitHonor(!RunCoordinator.Selected.FreeSummitHonor);
        pass.UpdateAscent();
    }

    private void ShowHonorButton(bool visible)
    {
        if (honorButton.gameObject.activeSelf == visible) return;
        var start = (RectTransform)pass.startGameButton.transform;
        if (visible)
        {
            fullPosition = start.anchoredPosition;
            fullSizeDelta = start.sizeDelta;
            // The native button animator writes localScale on reopen and hover.
            // Reserve a real row in the rectangle, keeping its top edge fixed.
            Rect original = start.rect;
            float rowHeight = original.height * HonorRowFraction;
            start.sizeDelta = fullSizeDelta - Vector2.up * rowHeight;
            start.anchoredPosition = fullPosition + Vector2.up * (rowHeight * (1f - start.pivot.y));

            var rect = (RectTransform)honorButton.transform;
            rect.anchorMin = rect.anchorMax = start.anchorMin + Vector2.Scale(start.anchorMax - start.anchorMin, start.pivot);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(original.width, rowHeight - ButtonGap);
            rect.anchoredPosition = fullPosition + new Vector2(original.center.x, original.yMin);
        }
        else
        {
            start.anchoredPosition = fullPosition;
            start.sizeDelta = fullSizeDelta;
        }
        honorButton.gameObject.SetActive(visible);
    }

    internal void Refresh()
    {
        var selected = RunCoordinator.Selected;
        displayedRules = selected.Encode();
        displayedCanEdit = CanEdit;
        ShowHonorButton(selected.IsExtended);
        if (selected.IsExtended)
        {
            pass.ascentTitle.text = "续作天阶 " + selected.Ascent;
            pass.ascentDesc.text = EffectDescriptions.BoardingSummary(selected.Ascent);
            pass.reward.SetActive(false);
            pass.incrementAscentButton.interactable = displayedCanEdit && selected.Ascent < DifficultySnapshot.LastLevel;
            pass.decrementAscentButton.interactable = displayedCanEdit;
            honorButton.interactable = displayedCanEdit;
            honorLabel.text = selected.FreeSummitHonor ? "免宝石通关：开" : "免宝石通关：关";
        }
        else if (displayedCanEdit && !RunSettings.IsCustomRun && pass.ascentIndex >= LastOfficial(pass))
        {
            pass.incrementAscentButton.interactable = true;
        }

        displayedMessage = StatusMessage(out bool blockStart);
        SetBlocked(pass.startGameButton, blockStart, ref startBlocked, ref startWasInteractable);
        SetBlocked(pass.customRunButton, selected.IsExtended && !displayedCanEdit, ref customBlocked, ref customWasInteractable);
        if (!string.IsNullOrEmpty(displayedMessage)) pass.ascentDesc.text = displayedMessage;
    }

    private static string StatusMessage(out bool blockStart)
    {
        blockStart = !RunCoordinator.AllReady(out string reason);
        return blockStart ? reason : RunCoordinator.Notice;
    }

    private static void SetBlocked(Button button, bool block, ref bool blocked, ref bool wasInteractable)
    {
        if (block)
        {
            if (!blocked) wasInteractable = button.interactable;
            button.interactable = false;
        }
        else if (blocked) button.interactable = wasInteractable;
        blocked = block;
    }

    private void Update()
    {
        if (!pass.isOpen || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.2f;
        string message = StatusMessage(out bool blockStart);
        if (displayedRules != RunCoordinator.Selected.Encode() || displayedCanEdit != CanEdit ||
            displayedMessage != message || startBlocked != blockStart)
            pass.UpdateAscent();
    }
}
