using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

internal static class BoardingPassLayoutRegression
{
    internal static int Run(Assembly mod)
    {
        var root = new GameObject("Inactive boarding pass fixture", typeof(RectTransform));
        root.SetActive(false);
        int checks = 0;
        try
        {
            var ticket = root.AddComponent(AccessTools.TypeByName("BoardingPass"));
            var buttonType = AccessTools.TypeByName("UnityEngine.UI.Button");
            var start = Child("StartGameButton", root.transform);
            start.sizeDelta = new Vector2(374f, 305.65f);
            start.anchoredPosition = new Vector2(437f, -33.475f);
            AccessTools.Field(ticket.GetType(), "startGameButton").SetValue(ticket, start.gameObject.AddComponent(buttonType));
            var template = Child("Custom options", root.transform);
            var nativeButton = template.gameObject.AddComponent(buttonType);
            Child("Text", template).gameObject.AddComponent(AccessTools.TypeByName("TMPro.TextMeshProUGUI"));
            AccessTools.Field(ticket.GetType(), "customOptionsButton").SetValue(ticket, nativeButton);
            var selector = root.AddComponent(mod.GetType("dda.AirportSelector"));
            AccessTools.Method(selector.GetType(), "Awake").Invoke(selector, null);
            var honor = (RectTransform)root.transform.Find("ContinuedFreeSummitHonor");
            var show = AccessTools.Method(selector.GetType(), "ShowHonorButton");
            var originalSize = start.sizeDelta;
            var originalPosition = start.anchoredPosition;
            for (int i = 0; i < 3; i++)
            {
                show.Invoke(selector, new object[] { true });
                // The native Animator writes this on reopen. Scaling the button alone
                // looks correct until the next frame, when both hit areas overlap.
                start.localScale = Vector3.one;
                var upper = new Vector3[4]; var lower = new Vector3[4];
                start.GetWorldCorners(upper); honor.GetWorldCorners(lower);
                Check(honor.gameObject.activeSelf && lower[2].y < upper[0].y, "toggle has its own row after animation");
                Check(Mathf.Approximately(upper[0].x, lower[0].x) && Mathf.Approximately(upper[2].x, lower[2].x), "both rows align on the stub");
                show.Invoke(selector, new object[] { false });
                Check(!honor.gameObject.activeSelf && start.sizeDelta == originalSize && start.anchoredPosition == originalPosition, "official layout restores on exit");
            }
            return checks;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }

        void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    }

    private static RectTransform Child(string name, Transform parent)
    {
        var child = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        child.SetParent(parent, false);
        return child;
    }
}
