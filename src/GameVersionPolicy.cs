using System;
using System.Globalization;

namespace dda;

public static class GameVersionPolicy
{
    public const string VerifiedGame = "3.0.0";
    public const string AllowedSeries = "3.0";
    public const int NativeSaveVersion = 4;

    // The audited Steam build contains this exact packaging suffix in version.txt.
    // Do not strip arbitrary command-like suffixes or alter the game file.
    public static string Normalize(string version) => version == "3.0.0--logFile" ? VerifiedGame : version;

    public static bool Allows(string version) => TryPatch(version, out _);

    private static bool TryPatch(string version, out int patch)
    {
        patch = 0;
        version = Normalize(version);
        if (version == null || !version.StartsWith("3.0.", StringComparison.Ordinal)) return false;
        string suffix = version.Substring(4);
        return int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out patch) &&
            patch >= 0 && suffix == patch.ToString(CultureInfo.InvariantCulture);
    }

    public static bool CanReadSave(string savedGame, string currentGame)
    {
        return TryPatch(savedGame, out int saved) && TryPatch(currentGame, out int current) && saved <= current;
    }
}
