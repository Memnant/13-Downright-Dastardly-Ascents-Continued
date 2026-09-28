using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

internal static class RegionalRecoveryRegression
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static object fixtureView, affliction;
    private static FieldInfo rateField, hotField;
    private static bool owned, throwOnSubtract, natural;
    private static int sleepCalls, hotCalls;
    private static float sleepRate, hotRate, sleepAmount;

    internal static int Run(Assembly mod)
    {
        var coordinator = mod.GetType("dda.RunCoordinator");
        var active = coordinator.GetField("Active", Flags);
        var running = coordinator.GetField("InRun", Flags);
        var characterType = AccessTools.TypeByName("Character");
        var dayType = AccessTools.TypeByName("DayNightManager");
        var mapType = AccessTools.TypeByName("MapHandler");
        var mapInstance = mapType.BaseType.GetField("_instance", Flags);
        var local = characterType.GetField("localCharacter", Flags);
        var dayInstance = dayType.GetField("instance", Flags);
        object oldActive = active.GetValue(null), oldRunning = running.GetValue(null), oldLocal = local.GetValue(null),
            oldDay = dayInstance.GetValue(null), oldMap = mapInstance.GetValue(null);
        var go = new GameObject("Continued regional recovery fixture");
        go.SetActive(false);
        var harness = new Harmony("dda.continued.recovery-fixture");
        int checks = 0;
        try
        {
            var viewType = AccessTools.TypeByName("Photon.Pun.PhotonView");
            fixtureView = go.AddComponent(viewType);
            var isMine = viewType.GetField("<IsMine>k__BackingField", Flags);
            isMine.SetValue(fixtureView, true); // Native getter may already be inlined in the patched body.
            var character = go.AddComponent(characterType);
            characterType.GetField("view", Flags).SetValue(character, fixtureView);
            var dataField = characterType.GetField("data", Flags);
            dataField.SetValue(character, Activator.CreateInstance(dataField.FieldType));
            local.SetValue(null, character);
            var day = go.AddComponent(dayType); dayInstance.SetValue(null, day);
            dayType.GetField("dayStart", Flags).SetValue(day, .25f);
            dayType.GetField("dayEnd", Flags).SetValue(day, .75f);
            var map = go.AddComponent(mapType); mapInstance.SetValue(null, map);
            var segmentType = mapType.GetNestedType("MapSegment");
            var segments = Array.CreateInstance(segmentType, 7);
            for (int i = 0; i < segments.Length; i++) segments.SetValue(Activator.CreateInstance(segmentType), i);
            mapType.GetField("segments", Flags).SetValue(map, segments);
            var biomeField = segmentType.GetField("_biome", Flags);

            var afflictionType = AccessTools.TypeByName("CharacterAfflictions");
            affliction = go.AddComponent(afflictionType);
            afflictionType.GetField("character", Flags).SetValue(affliction, character);
            var statuses = new float[20];
            var lastAdded = new float[20];
            afflictionType.GetField("currentStatuses", Flags).SetValue(affliction, statuses);
            afflictionType.GetField("lastAddedStatus", Flags).SetValue(affliction, lastAdded);
            rateField = afflictionType.GetField("drowsyReductionPerSecond", Flags);
            hotField = afflictionType.GetField("hotReductionPerSecond", Flags);
            var cooldown = afflictionType.GetField("drowsyReductionCooldown", Flags);
            cooldown.SetValue(affliction, 5f);
            afflictionType.GetField("hotReductionCooldown", Flags).SetValue(affliction, 5f);
            var statusType = afflictionType.GetNestedType("STATUSTYPE");
            object drowsy = Enum.Parse(statusType, "Drowsy");
            int drowsyIndex = (int)drowsy, hotIndex = (int)Enum.Parse(statusType, "Hot");
            statuses[drowsyIndex] = .5f; statuses[hotIndex] = .2f;
            var update = afflictionType.GetMethod("UpdateNormalStatuses", Flags);
            var subtract = afflictionType.GetMethod("SubtractStatus", Flags);
            // Run the actual native recovery/cooldown branches. Isolate ownership and
            // the final status delivery (HUD/network), and disable unrelated hunger/cold.
            harness.Patch(AccessTools.PropertyGetter(viewType, "IsMine"), prefix: new HarmonyMethod(typeof(RegionalRecoveryRegression), "Ownership"));
            harness.Patch(AccessTools.PropertyGetter(dataField.FieldType, "fullyConscious"), prefix: new HarmonyMethod(typeof(RegionalRecoveryRegression), "NoAmbient"));
            harness.Patch(AccessTools.PropertyGetter(AccessTools.TypeByName("Ascents"), "isNightCold"), prefix: new HarmonyMethod(typeof(RegionalRecoveryRegression), "NoAmbient"));
            harness.Patch(subtract, prefix: new HarmonyMethod(typeof(RegionalRecoveryRegression), "CaptureSubtract"));
            running.SetValue(null, true); owned = true;

            foreach (var selection in new[] { (0, 0), (15, 0), (16, 0), (17, 0), (18, 0), (19, 0), (20, 0),
                (0, 1 << 9), (0, 1 << 11), (0, (1 << 9) | (1 << 11)), (19, 1 << 11) })
            foreach (var region in new[] { (8, 3), (8, 4), (9, 4), (3, 3), (3, 4), (8, 2), (9, 3), (2, 2), (0, 0) })
            foreach (bool night in new[] { false, true })
            {
                int level = selection.Item1, mask = selection.Item2;
                SetSelection(level, mask);
                SetRegion(region.Item1, region.Item2);
                SetDay(night);
                rateField.SetValue(affliction, .07f); hotField.SetValue(affliction, .01f);
                lastAdded[drowsyIndex] = lastAdded[hotIndex] = -100f;
                bool level16 = level >= 16 || (mask & (1 << 7)) != 0;
                bool level18 = level >= 18 || (mask & (1 << 9)) != 0;
                bool level20 = level == 20 || (mask & (1 << 11)) != 0;
                bool sleepRegion = (region.Item1 == 8 && (region.Item2 == 3 || region.Item2 == 4)) || (region.Item1 == 9 && region.Item2 == 4);
                float expectedSleep = level16 ? (night ? 0 : .02f) : .07f;
                if (level18 && sleepRegion && !(level16 && night))
                    expectedSleep = level20 && region.Item1 == 8 && region.Item2 == 3 ? .02f / 3f : .00333333f;
                float expectedHot = level18 && region.Item1 == 3 && (region.Item2 == 3 || region.Item2 == 4) ? .00333333f : .01f;
                ResetCounters(); update.Invoke(affliction, null);
                Assert(sleepCalls == 1 && Math.Abs(sleepRate - expectedSleep) < 1e-8f, "native sleep rate: " + level + "/" + region + "/night=" + night + "/calls=" + sleepCalls + "/rate=" + sleepRate);
                Assert(hotCalls == 1 && Math.Abs(hotRate - expectedHot) < 1e-8f, "legacy volcano/kiln hot recovery unchanged");
                Assert(natural && Math.Abs(sleepAmount - expectedSleep * Time.deltaTime) < 1e-7f, "native recovery uses rate times deltaTime and natural flag");
                Assert((float)rateField.GetValue(affliction) == .07f && (float)hotField.GetValue(affliction) == .01f, "rates restored between ticks and region transitions");
            }

            SetSelection(18); SetRegion(8, 4); SetDay(false);
            lastAdded[drowsyIndex] = Time.time;
            ResetCounters(); update.Invoke(affliction, null);
            Assert(sleepCalls == 0, "recent drowsiness still waits for native cooldown");
            lastAdded[drowsyIndex] = -100f;
            ResetCounters(); update.Invoke(affliction, null);
            Assert(sleepCalls == 1 && sleepRate == .00333333f, "recovery starts when the source cooldown expires");
            for (int i = 0; i < 3; i++)
            {
                ResetCounters(); update.Invoke(affliction, null);
                Assert(sleepRate == .00333333f && (float)rateField.GetValue(affliction) == .07f, "repeated calls never compound rate");
            }
            SetDay(true); ResetCounters(); update.Invoke(affliction, null);
            Assert(sleepRate == 0f, "restored daytime checkpoint entering night still stops natural recovery");
            SetDay(false); ResetCounters(); update.Invoke(affliction, null);
            Assert(sleepRate == .00333333f, "daybreak restores regional recovery");
            ResetCounters(); subtract.Invoke(affliction, new object[] { drowsy, .15f, false, false });
            Assert(sleepAmount == .15f && !natural, "direct item recovery receives no new regional scaling");
            SetRegion(2, 2); ResetCounters(); update.Invoke(affliction, null);
            Assert(sleepRate == .02f, "leaving regional biome restores normal level 16 day rate");
            SetRegion(8, 3); mapInstance.SetValue(null, null); ResetCounters(); update.Invoke(affliction, null);
            Assert(sleepRate == .02f, "missing map does not apply regional penalty");
            mapInstance.SetValue(null, map); dayInstance.SetValue(null, null); ResetCounters(); update.Invoke(affliction, null);
            Assert(sleepRate == .00333333f, "regional recovery works before day manager appears");
            dayInstance.SetValue(null, day);
            running.SetValue(null, false); ResetCounters(); update.Invoke(affliction, null);
            Assert(sleepRate == .07f, "outside a run all regional rules are inactive");
            running.SetValue(null, true); local.SetValue(null, null); owned = false; isMine.SetValue(fixtureView, false); ResetCounters(); update.Invoke(affliction, null);
            Assert(sleepCalls == 0 && (float)rateField.GetValue(affliction) == .07f, "remote character is not ticked twice");
            local.SetValue(null, character); owned = true; isMine.SetValue(fixtureView, true); throwOnSubtract = true;
            bool threw = false;
            try { update.Invoke(affliction, null); }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { threw = true; }
            Assert(threw && (float)rateField.GetValue(affliction) == .07f && (float)hotField.GetValue(affliction) == .01f, "exception restores both rates");
            return checks;

            void SetSelection(int level, int mask = 0) => active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), level, mask, true, true));
            void SetRegion(int biome, int segment)
            {
                mapType.GetField("currentSegment", Flags).SetValue(map, segment);
                biomeField.SetValue(segments.GetValue(segment), Enum.ToObject(biomeField.FieldType, biome));
            }
            void SetDay(bool night) => dayType.GetField("timeOfDay", Flags).SetValue(day, night ? .1f : .5f);
        }
        finally
        {
            harness.UnpatchSelf(); fixtureView = affliction = null; throwOnSubtract = false;
            active.SetValue(null, oldActive); running.SetValue(null, oldRunning); local.SetValue(null, oldLocal);
            dayInstance.SetValue(null, oldDay); mapInstance.SetValue(null, oldMap);
            UnityEngine.Object.DestroyImmediate(go);
        }
        void Assert(bool pass, string reason) { if (!pass) throw new Exception(reason); checks++; }
    }
    private static void ResetCounters() { sleepCalls = hotCalls = 0; sleepRate = hotRate = sleepAmount = -1; natural = false; }
    private static bool Ownership(object __instance, ref bool __result)
    { if (!ReferenceEquals(__instance, fixtureView)) return true; __result = owned; return false; }
    private static bool NoAmbient(ref bool __result) { __result = false; return false; }
    private static bool CaptureSubtract(object __instance, object statusType, float amount, bool decreasedNaturally)
    {
        if (!ReferenceEquals(__instance, affliction)) return true;
        if (throwOnSubtract) throw new InvalidOperationException("fixture");
        if (statusType.ToString() == "Drowsy")
        {
            sleepCalls++; sleepRate = (float)rateField.GetValue(affliction); sleepAmount = amount; natural = decreasedNaturally;
        }
        if (statusType.ToString() == "Hot") { hotCalls++; hotRate = (float)hotField.GetValue(affliction); }
        return false;
    }
}
