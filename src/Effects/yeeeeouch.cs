// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using System;
using System.Globalization;
using HarmonyLib;

namespace dda;

[HarmonyPatch(typeof(Luggage), "Interact_CastFinished")]
public static class yeeeeouch
{
    internal static void Prefix(Luggage __instance, Character interactor)
    {
        if (!Rules.Enabled(11) || __instance.IsOpen) return;
        string kind = PrefabName(__instance.name);
        float injury = kind switch {
            "LuggageSmall" => .06f,
            "LuggageEpic" => .16f,
            "scout statue" => .20f,
            _ => .10f
        };
        var afflictions = interactor.refs.afflictions;
        afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Injury, injury, false, true, true);
        if (kind == "scout statue")
            afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Curse, .06f, false, true, true);
    }

    private static string PrefabName(string name)
    {
        if (name.EndsWith("(Clone)", StringComparison.Ordinal))
            name = name.Substring(0, name.Length - "(Clone)".Length);
        int suffix = name.LastIndexOf(" (", StringComparison.Ordinal);
        if (suffix >= 0 && name.EndsWith(")", StringComparison.Ordinal) &&
            int.TryParse(name.AsSpan(suffix + 2, name.Length - suffix - 3), NumberStyles.None, CultureInfo.InvariantCulture, out _))
            return name.Substring(0, suffix);
        return name;
    }
}
