using System;
using System.Globalization;

namespace dda
{
    /// <summary>Stable level IDs; removed legacy levels never occupy a new effect's slot.</summary>
    public sealed class DifficultySnapshot
    {
        public const int SchemaVersion = 2;
        public const int FirstLevel = 9;
        public const int LastLevel = 20;
        public const int AllowedMask = (1 << 12) - 1;
        public static readonly DifficultySnapshot Official = new DifficultySnapshot(0, 0, true, true);

        public int Ascent { get; }
        public int ForceMask { get; }
        public bool NerfedRevives { get; }
        public bool CursePunishment { get; }
        public bool FreeSummitHonor { get; }
        public bool GrantsSummitHonor => IsExtended && FreeSummitHonor;
        private string encoded;
        public bool IsExtended => Ascent >= FirstLevel;
        public bool HasEffects => IsExtended || ForceMask != 0;

        public DifficultySnapshot(int ascent, int forceMask, bool nerfedRevives, bool cursePunishment)
            : this(ascent, forceMask, nerfedRevives, cursePunishment, false) { }

        public DifficultySnapshot(int ascent, int forceMask, bool nerfedRevives, bool cursePunishment, bool freeSummitHonor)
        {
            if (ascent != 0 && (ascent < FirstLevel || ascent > LastLevel))
                throw new ArgumentOutOfRangeException(nameof(ascent));
            if (forceMask < 0 || (forceMask & ~AllowedMask) != 0)
                throw new ArgumentOutOfRangeException(nameof(forceMask));
            Ascent = ascent;
            ForceMask = forceMask;
            NerfedRevives = nerfedRevives;
            CursePunishment = cursePunishment;
            FreeSummitHonor = freeSummitHonor;
        }

        public bool Enabled(int level)
        {
            return level >= FirstLevel && level <= LastLevel &&
                   (Ascent >= level || (ForceMask & (1 << (level - FirstLevel))) != 0);
        }

        public string Encode() => encoded ??= string.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2}:{3}:{4}:{5}",
            SchemaVersion, Ascent, ForceMask, NerfedRevives ? 1 : 0, CursePunishment ? 1 : 0, FreeSummitHonor ? 1 : 0);

        public static bool TryDecode(string text, out DifficultySnapshot value)
        {
            value = Official;
            if (string.IsNullOrEmpty(text)) return false;
            string[] parts = text.Split(':');
            if (parts.Length != 5 && parts.Length != 6) return false;
            int[] data = new int[parts.Length];
            for (int i = 0; i < data.Length; i++)
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out data[i])) return false;
            bool legacy = data[0] == 1 && parts.Length == 5;
            if ((!legacy && (data[0] != SchemaVersion || parts.Length != 6)) || data[3] > 1 || data[4] > 1 ||
                (!legacy && data[5] > 1)) return false;
            try { value = new DifficultySnapshot(data[1], data[2], data[3] == 1, data[4] == 1, !legacy && data[5] == 1); return true; }
            catch (ArgumentOutOfRangeException) { return false; }
        }

        public static int MigrateLegacyMask(bool[] oldFlags)
        {
            int mask = 0;
            if (oldFlags == null) return mask;
            // Legacy slot 0 was Ascent 8; slots 1..12 were Ascents 9..20.
            for (int i = 1; i <= 12 && i < oldFlags.Length; i++)
                if (oldFlags[i]) mask |= 1 << (i - 1);
            return mask;
        }
    }
}
