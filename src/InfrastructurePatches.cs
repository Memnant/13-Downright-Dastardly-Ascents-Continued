using HarmonyLib;
using Peak;
using UnityEngine;

namespace dda;

[HarmonyPatch(typeof(AirportCheckInKiosk), "LoadIslandMaster")]
internal static class ContinuedDeparture
{
    private static bool Prefix(ref int ascent, ref byte[] serializedRunSettings) =>
        RunCoordinator.PrepareDeparture(ref ascent, ref serializedRunSettings);
}

[HarmonyPatch(typeof(AirportCheckInKiosk), "BeginIslandLoadRPC")]
internal static class ContinuedArrival
{
    private static void Prefix(int ascent) => RunCoordinator.ReceiveDeparture(ascent);
}

[HarmonyPatch(typeof(AchievementManager), "TryCompleteAscent")]
internal static class IndependentAscentProgress
{
    private static bool Prefix() => !Rules.Snapshot.IsExtended;
}

[HarmonyPatch(typeof(AscentUI), "Start")]
internal static class ContinuedHud
{
    private static void Postfix(AscentUI __instance)
    {
        if (Rules.Snapshot.IsExtended && __instance.text != null)
            __instance.text.text = "续作天阶 " + Rules.Level;
    }
}

[HarmonyPatch(typeof(Quicksave), "SaveNow")]
internal static class ContinuedQuicksave
{
    private static void Postfix()
    {
        try { RunSaveStore.SaveCheckpoint(); }
        catch (System.Exception error) { Plugin.Log.LogError("Native save succeeded but Continued metadata did not save: " + error); }
    }
}

[HarmonyPatch(typeof(Quicksave), "LoadSavedGameScene")]
internal static class ContinuedResume
{
    private static bool Prefix() => RunSaveStore.PrepareResume(Quicksave.SavedRunId, Quicksave.SavedRun.ascent);
}

[HarmonyPatch(typeof(Character), "ReviveCharacter")]
internal static class ContinuedRevivalPenalty
{
    private static void Prefix(Character __instance, out bool __state)
    {
        TornadoRecovery.Forget(__instance);
        __state = Rules.Enabled(12) && Rules.Owns(__instance) &&
            (__instance.data.dead || __instance.data.passedOut || __instance.data.fullyPassedOut);
    }
    private static void Postfix(Character __instance, bool applyStatus, bool __state)
    {
        if (!__state || !Rules.Snapshot.NerfedRevives || applyStatus) return;
        // The current game's complete revival (collisions, petrify, thorns, etc.) runs first.
        __instance.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Curse, 0.1f);
        __instance.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Hunger, 0.2f);
    }
}

[HarmonyPatch(typeof(Quicksave), "FinalizeRunSetup")]
internal static class ContinuedResumeFinished
{
    private static void Postfix()
    {
        RunCoordinator.ResumePrepared = false;
        TideSync.Tick();
        WeatherGrace.Tick();
    }
}
