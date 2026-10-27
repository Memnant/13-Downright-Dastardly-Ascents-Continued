using System;
using System.Globalization;

namespace dda;

public static class GameVersionPolicy
{
    public const string VerifiedGame = "3.0.1";
    public const string AllowedSeries = "3.x";
    public const int NativeSaveVersion = 4;
    private const string PackagingSuffix = "--logFile";

    // Steam builds of 3.0.x (3.0.0 and 3.0.1 so far) write version.txt as "<version>--logFile".
    // Strip exactly that suffix, and only from an otherwise valid 3.x numeric version.
    // Do not strip arbitrary command-like suffixes or alter the game file.
    public static string Normalize(string version)
    {
        if (version != null && version.EndsWith(PackagingSuffix, StringComparison.Ordinal))
        {
            string bare = version.Substring(0, version.Length - PackagingSuffix.Length);
            if (IsNumeric3x(bare, out _, out _)) return bare;
        }
        return version;
    }

    // Any numeric 3.x.y may try to load; the startup interface and native-save checks decide.
    // Major 4 or later needs a separate review.
    public static bool Allows(string version) => IsNumeric3x(Normalize(version), out _, out _);

    private static bool IsNumeric3x(string version, out int minor, out int patch)
    {
        minor = patch = 0;
        if (version == null || !version.StartsWith("3.", StringComparison.Ordinal)) return false;
        string[] parts = version.Substring(2).Split('.');
        return parts.Length == 2 && Number(parts[0], out minor) && Number(parts[1], out patch);
    }

    private static bool Number(string text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
        value >= 0 && text == value.ToString(CultureInfo.InvariantCulture);

    // Same native save format is checked separately; here only forbid a game-version downgrade.
    public static bool CanReadSave(string savedGame, string currentGame)
    {
        return IsNumeric3x(Normalize(savedGame), out int savedMinor, out int savedPatch) &&
            IsNumeric3x(Normalize(currentGame), out int minor, out int patch) &&
            (savedMinor < minor || (savedMinor == minor && savedPatch <= patch));
    }
}
