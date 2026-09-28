using HarmonyLib;

namespace dda;

[HarmonyPatch(typeof(CharacterAfflictions), "AddStatus")]
internal static class moreEffect
{
    private static void Prefix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, ref float amount)
    {
        if (amount <= 0 || StatusReplication.Active || !Rules.Owns(__instance.character)) return;
        if (Rules.Enabled(17))
        {
            if (statusType == CharacterAfflictions.STATUSTYPE.Cold ||
                (statusType == CharacterAfflictions.STATUSTYPE.Drowsy && !Tier20Swamp.Active)) amount *= 1.3f;
            else if (statusType == CharacterAfflictions.STATUSTYPE.Hot) amount *= 1.5f;
            else if (statusType == CharacterAfflictions.STATUSTYPE.Poison && !CoastalPoison.IsFixedFor(__instance)) amount *= 1.75f;
        }
        if (Rules.Enabled(9) && statusType == CharacterAfflictions.STATUSTYPE.Spores) amount *= 1.65f;
    }
}
