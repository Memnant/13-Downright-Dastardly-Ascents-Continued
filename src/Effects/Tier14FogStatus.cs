using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Zorro.Core;

namespace dda;

// Keep the game's fog enabled/radius checks, rendering, isInFog flag and AddStatus
// behavior. Only the two status calls belonging to this fog are redirected.
[HarmonyPatch(typeof(FogSphere), "SetSharderVars")]
internal static class Tier14FogStatus
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.Method(typeof(CharacterAfflictions), nameof(CharacterAfflictions.AddStatus),
            new[] { typeof(CharacterAfflictions.STATUSTYPE), typeof(float), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
        var replacement = AccessTools.Method(typeof(Tier14FogStatus), nameof(AddFogStatus));
        var code = new List<CodeInstruction>(instructions);
        int replaced = 0;
        foreach (var instruction in code)
        {
            if (!instruction.Calls(original)) continue;
            instruction.opcode = OpCodes.Call;
            instruction.operand = replacement;
            replaced++;
        }
        if (replaced != 2) throw new InvalidOperationException("Expected two native FogSphere status calls; found " + replaced);
        return code;
    }

    private static bool AddFogStatus(CharacterAfflictions recipient, CharacterAfflictions.STATUSTYPE statusType,
        float amount, bool fromRPC, bool playEffects, bool notify, bool ignoreInvincibility)
    {
        if (SwampChasingFog.ActiveRegion)
            return SwampChasingFog.AddStatus(recipient, fromRPC, playEffects, notify, ignoreInvincibility);
        if (amount > 0 && Rules.Enabled(14) && Rules.Owns(recipient.character) && !StatusReplication.Active &&
            TryGetBiome(out var biome))
        {
            if (statusType == CharacterAfflictions.STATUSTYPE.Cold)
            {
                bool hot = biome == Biome.BiomeType.Volcano;
                statusType = hot ? CharacterAfflictions.STATUSTYPE.Hot : CharacterAfflictions.STATUSTYPE.Poison;
                amount *= hot ? 6f : 3f; // Old 0.0105 * 3; Volcano additionally * 2.
            }
            else if (statusType == CharacterAfflictions.STATUSTYPE.Injury)
                amount *= 3f; // Native skeleton base is already divided by eight.
        }
        // PEAK 2.4.c sets isInFog AFTER AddStatus, whereas the old mod set it before.
        // Let the existing tier-12 skeleton rule recognize this exact fog injury;
        // otherwise it treats it as ordinary injury and multiplies it by 99.
        var data = recipient.character.data;
        bool markFog = Rules.Enabled(12) && Rules.Owns(recipient.character) && data.isSkeleton && !data.isInFog;
        if (markFog) data.isInFog = true;
        try
        {
            // Normal immunity, status limits, networking and tier 17 still run once.
            return recipient.AddStatus(statusType, amount, fromRPC, playEffects, notify, ignoreInvincibility);
        }
        finally { if (markFog) data.isInFog = false; }
    }

    private static bool TryGetBiome(out Biome.BiomeType biome)
    {
        biome = default;
        if (!MapHandler.Exists) return false;
        var map = Singleton<MapHandler>.Instance;
        if (map.segments == null || map.currentSegment < 0 || map.currentSegment >= map.segments.Length ||
            map.segments[map.currentSegment] == null) return false;
        biome = map.GetCurrentBiome();
        return true;
    }
}
