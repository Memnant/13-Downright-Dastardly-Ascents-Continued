using HarmonyLib;

namespace dda;

[HarmonyPatch(typeof(Character), "RPCA_Die")]
internal static class dontDie
{
    private static void Prefix(Character __instance, out bool __state) => __state = __instance.data.dead;
    private static void Postfix(Character __instance, bool __state)
    {
        if (__state || !Rules.Enabled(10) || !Rules.Snapshot.CursePunishment) return;
        var local = Character.localCharacter;
        if (local == null || local == __instance || local.data.dead) return;
        int count = Character.AllCharacters.Count;
        float amount = count <= 2 ? 0.12f : count <= 4 ? 0.08f : count <= 6 ? 0.04f : 0.02f;
        local.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Curse, amount);
    }
}
