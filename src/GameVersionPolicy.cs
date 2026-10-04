using System;

namespace dda;

public static class GameVersionPolicy
{
    public const string VerifiedGame = "2.6.a";
    public const string AllowedSeries = "2.6";
    public const int NativeSaveVersion = 4;

    // PEAK uses lower-case letter patch suffixes. Reject malformed versions and
    // other series instead of treating a prefix such as 2.60 as 2.6.
    public static bool Allows(string version)
    {
        if (version == null || !version.StartsWith("2.6.", StringComparison.Ordinal) || version.Length < 5) return false;
        for (int i = 4; i < version.Length; i++)
            if (version[i] < 'a' || version[i] > 'z') return false;
        return true;
    }

    public static bool CanReadSave(string savedGame, string currentGame)
    {
        if (!Allows(savedGame) || !Allows(currentGame)) return false;
        // Permit forward patch updates only; never silently downgrade a save.
        return savedGame.Length < currentGame.Length ||
            (savedGame.Length == currentGame.Length && string.CompareOrdinal(savedGame, currentGame) <= 0);
    }
}
