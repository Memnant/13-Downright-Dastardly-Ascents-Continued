using System;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using UnityEngine;

internal static class SurfaceRegression
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static object capturedAffliction;
    private static int petrify, statusCalls, callbacks, hazard;
    private static float statusAmount;
    private static bool throwInCallback, throwOnStatus;
    private static object itemAction, itemCharacter;

    internal static int Run(Assembly mod)
    {
        var coordinator = mod.GetType("dda.RunCoordinator");
        var active = coordinator.GetField("Active", Flags);
        var running = coordinator.GetField("InRun", Flags);
        var characterType = AccessTools.TypeByName("Character");
        var local = characterType.GetField("localCharacter", Flags);
        object oldActive = active.GetValue(null), oldRunning = running.GetValue(null), oldLocal = local.GetValue(null);
        var fixture = new GameObject("Continued stone regression fixture");
        fixture.SetActive(false);
        var collisionFixture = new GameObject("Continued collision regression fixture");
        var urchin = new GameObject("Urch"); collisionFixture.transform.SetParent(urchin.transform, false);
        var harness = new Harmony("dda.continued.surface-fixture");
        int checks = 0;
        try
        {
            var character = fixture.AddComponent(characterType);
            var dataField = characterType.GetField("data", Flags);
            dataField.SetValue(character, Activator.CreateInstance(dataField.FieldType));
            local.SetValue(null, character);
            var afflictionType = AccessTools.TypeByName("CharacterAfflictions");
            capturedAffliction = FormatterServices.GetUninitializedObject(afflictionType);
            afflictionType.GetField("character", Flags).SetValue(capturedAffliction, character);
            afflictionType.GetField("currentStatuses", Flags).SetValue(capturedAffliction, new float[20]);
            var refsField = characterType.GetField("refs", Flags);
            var refs = Activator.CreateInstance(refsField.FieldType);
            refsField.FieldType.GetField("afflictions", Flags).SetValue(refs, capturedAffliction);
            refsField.SetValue(character, refs);
            var surfaceType = AccessTools.TypeByName("ClimbModifierSurface");
            var surface = fixture.AddComponent(surfaceType);
            var onClimb = surfaceType.GetMethod("OnClimb", Flags);
            surfaceType.GetField("applyStatus", Flags).SetValue(surface, true);
            surfaceType.GetField("statusCooldown", Flags).SetValue(surface, .5f);
            var statusType = surfaceType.GetField("statusType", Flags);
            statusType.SetValue(surface, Enum.Parse(statusType.FieldType, "Hunger"));
            var callback = surfaceType.GetField("onClimbAction", Flags);
            callback.SetValue(surface, Delegate.CreateDelegate(callback.FieldType, typeof(SurfaceRegression).GetMethod("OnClimb", Flags)));
            var runSettings = AccessTools.TypeByName("RunSettings");
            var setting = runSettings.GetNestedType("SETTINGTYPE");
            harness.Patch(AccessTools.Method(runSettings, "GetValue", new[] { setting, typeof(bool) }),
                prefix: new HarmonyMethod(typeof(SurfaceRegression), "CaptureHazard"));
            harness.Patch(AccessTools.Method(afflictionType, "AddPetrify", new[] { typeof(int) }),
                prefix: new HarmonyMethod(typeof(SurfaceRegression), "CapturePetrify"));
            harness.Patch(AccessTools.Method(afflictionType, "AddStatus"),
                prefix: new HarmonyMethod(typeof(SurfaceRegression), "CaptureStatus") { priority = Priority.Last });
            var collisionType = AccessTools.TypeByName("CollisionModifier");
            var collision = collisionFixture.AddComponent(collisionType);
            var collide = collisionType.GetMethod("Collide", Flags);
            collisionType.GetField("applyPetrify", Flags).SetValue(collision, true);
            collisionType.GetField("applyEffects", Flags).SetValue(collision, true);
            collisionType.GetField("statusType", Flags).SetValue(collision, Enum.Parse(statusType.FieldType, "Cold"));
            collisionType.GetField("damage", Flags).SetValue(collision, 2f);
            collisionType.GetField("cooldown", Flags).SetValue(collision, 1f);
            collisionType.GetField("knockback", Flags).SetValue(collision, 0f);
            var collisionCooldown = (System.Collections.IList)collisionType.GetField("characterList", Flags).GetValue(collision);
            object contact = Activator.CreateInstance(collide.GetParameters()[1].ParameterType);

            var actionType = AccessTools.TypeByName("Action_ModifyStatus");
            itemAction = fixture.AddComponent(actionType); itemCharacter = character;
            actionType.GetField("statusType", Flags).SetValue(itemAction, Enum.Parse(statusType.FieldType, "Petrify"));
            actionType.GetField("changeAmount", Flags).SetValue(itemAction, .3f);
            harness.Patch(AccessTools.PropertyGetter(AccessTools.TypeByName("ItemActionBase"), "character"),
                prefix: new HarmonyMethod(typeof(SurfaceRegression), "CaptureItemCharacter"));
            running.SetValue(null, true);
            foreach (int level in new[] { 0, 9, 15, 20 })
            {
                active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), level, 0, true, true));
                foreach (bool petrifying in new[] { false, true })
                {
                    ResetCounters(); hazard = 1;
                    Set("applyPetrify", petrifying); Set("statusAmount", petrifying ? 5f : .1f); Set("lastTriggerTime", -100f);
                    onClimb.Invoke(surface, new object[] { character });
                    Assert(callbacks == 1, "native climb callback runs exactly once");
                    Assert(petrifying ? petrify == 5 && statusCalls == 0 : petrify == 0 && statusCalls == 1,
                        "native petrification branch kept separate from status branch");
                    if (!petrifying) Assert(Math.Abs(statusAmount - (level >= 9 ? .2f : .1f)) < .00001f, "legacy rock multiplier preserved");
                    Assert((float)surfaceType.GetField("statusAmount", Flags).GetValue(surface) == (petrifying ? 5f : .1f), "surface amount restored");
                    onClimb.Invoke(surface, new object[] { character });
                    Assert(callbacks == 2 && (petrifying ? petrify == 5 : statusCalls == 1), "native cooldown prevents duplicate damage");
                }
                ResetCounters(); hazard = 0; Set("applyPetrify", true); Set("statusAmount", 5f); Set("lastTriggerTime", -100f);
                onClimb.Invoke(surface, new object[] { character });
                Assert(petrify == 0 && statusCalls == 0, "disabled native stone hazard respected");
                hazard = 1; local.SetValue(null, null); Set("lastTriggerTime", -100f);
                onClimb.Invoke(surface, new object[] { character });
                Assert(petrify == 0 && statusCalls == 0, "remote character receives no local duplicate");
                local.SetValue(null, character);

                // These are the real Level_3 stone fields: Cold, applyPetrify=true, damage=2, cooldown=1.
                hazard = 1; ResetCounters(); collisionCooldown.Clear();
                collide.Invoke(collision, new[] { character, contact, null, null });
                Assert(petrify == 2 && statusCalls == 0, "native collision adds 2 petrification points at level " + level);
                Assert((float)collisionType.GetField("damage", Flags).GetValue(collision) == 2f, "integer stone amount never rewritten to .02");
                collide.Invoke(collision, new[] { character, contact, null, null });
                Assert(petrify == 2 && collisionCooldown.Count == 1, "native collision cooldown prevents duplicate body-part hits");
                hazard = 0; ResetCounters(); collisionCooldown.Clear();
                collide.Invoke(collision, new[] { character, contact, null, null });
                Assert(petrify == 0, "collision honors native petrifying hazard toggle");

                // Execute the actual native item action; intercept the status delivery boundary only.
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    ResetCounters();
                    actionType.GetMethod("RunAction", Flags).Invoke(itemAction, null);
                    Assert(statusCalls == 1 && statusAmount == .3f, "Scout's Ambition native .3 status cost survives item modifier");
                    Assert(Mathf.FloorToInt(statusAmount * 100f) == 30, "native petrification conversion yields 30 points");
                    Assert((float)actionType.GetField("changeAmount", Flags).GetValue(itemAction) == .3f, "repeated use leaves item parameter unchanged");
                }
            }
            // Execute the native sea-urchin contact/cooldown; capture only status delivery.
            var poison = Enum.Parse(statusType.FieldType, "Poison");
            var addStatus = afflictionType.GetMethod("AddStatus", Flags);
            collisionType.GetField("applyPetrify", Flags).SetValue(collision, false);
            collisionType.GetField("statusType", Flags).SetValue(collision, poison);
            collisionType.GetField("damage", Flags).SetValue(collision, .1f);
            var jellyfish = mod.GetType("dda.CoastalPoison").GetMethod("ApplyJellyfish", Flags);
            foreach (int level in new[] { 0, 9, 16, 17, 20 })
            {
                active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), level, 0, true, true));
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    ResetCounters(); collisionCooldown.Clear();
                    collide.Invoke(collision, new[] { character, contact, null, null });
                    Assert(statusCalls == 1 && Math.Abs(statusAmount - (level >= 9 ? .4f : .1f)) < .00001f,
                        "urchin delivers exactly 40 points at continued levels, including level 17+; official contact untouched");
                    Assert((float)collisionType.GetField("damage", Flags).GetValue(collision) == .1f, "urchin damage restored after every contact");
                    collide.Invoke(collision, new[] { character, contact, null, null });
                    Assert(statusCalls == 1, "urchin native cooldown still prevents duplicate limb contacts");
                    if (level >= 9)
                    {
                        ResetCounters(); jellyfish.Invoke(null, new[] { character });
                        Assert(statusCalls == 1 && Math.Abs(statusAmount - .4f) < .00001f, "jellyfish delivery excludes tier-17 poison amplification");
                    }
                    ResetCounters(); addStatus.Invoke(capturedAffliction, new object[] { poison, .2f, true, false, false, false });
                    Assert(statusCalls == 1 && Math.Abs(statusAmount - (level >= 17 ? .35f : .2f)) < .00001f,
                        "unrelated poison still receives tier 17 after either coastal source returns: level=" + level + ", delivered=" + statusAmount);
                }
            }
            ResetCounters(); collisionCooldown.Clear(); throwOnStatus = true;
            bool contactThrew = false;
            try { collide.Invoke(collision, new[] { character, contact, null, null }); }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { contactThrew = true; }
            throwOnStatus = false;
            Assert(contactThrew && (float)collisionType.GetField("damage", Flags).GetValue(collision) == .1f, "failed contact restores damage");
            ResetCounters(); addStatus.Invoke(capturedAffliction, new object[] { poison, .2f, true, false, false, false });
            Assert(Math.Abs(statusAmount - .35f) < .00001f, "failed contact cannot leak the coastal exception to later poison");

            Set("applyPetrify", false); Set("statusAmount", .1f); throwInCallback = true;
            bool threw = false;
            try { onClimb.Invoke(surface, new object[] { character }); }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { threw = true; }
            Assert(threw && (float)surfaceType.GetField("statusAmount", Flags).GetValue(surface) == .1f, "exception path restores rock amount");
            return checks;
            void Set(string name, object value) => surfaceType.GetField(name, Flags).SetValue(surface, value);
        }
        finally
        {
            harness.UnpatchSelf(); capturedAffliction = null; throwInCallback = throwOnStatus = false;
            itemAction = itemCharacter = null;
            active.SetValue(null, oldActive); running.SetValue(null, oldRunning); local.SetValue(null, oldLocal);
            UnityEngine.Object.DestroyImmediate(fixture);
            UnityEngine.Object.DestroyImmediate(collisionFixture);
            UnityEngine.Object.DestroyImmediate(urchin);
        }
        void Assert(bool pass, string reason) { if (!pass) throw new Exception(reason); checks++; }
    }
    private static void ResetCounters() { petrify = statusCalls = callbacks = 0; statusAmount = 0; }
    private static void OnClimb(object character) { callbacks++; if (throwInCallback) throw new InvalidOperationException("fixture"); }
    private static bool CaptureHazard(object setting, ref int __result)
    { if (setting.ToString() != "Hazard_PetrifyingStones") return true; __result = hazard; return false; }
    private static bool CapturePetrify(object __instance, int petrify)
    { if (!ReferenceEquals(__instance, capturedAffliction)) return true; SurfaceRegression.petrify += petrify; return false; }
    private static bool CaptureStatus(object __instance, float amount, ref bool __result)
    { if (!ReferenceEquals(__instance, capturedAffliction)) return true; if (throwOnStatus) throw new InvalidOperationException("fixture status delivery"); statusCalls++; statusAmount = amount; __result = false; return false; }
    private static bool CaptureItemCharacter(object __instance, ref object __result)
    { if (!ReferenceEquals(__instance, itemAction)) return true; __result = itemCharacter; return false; }
}
