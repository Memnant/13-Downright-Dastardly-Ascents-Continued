using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

internal static class TornadoRecoveryRegression
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static object character, data;
    private static int injuryCalls, fallCalls;
    private static float injury, fallDuration;
    private static bool failEmote, failEquip;

    internal static int Run(Assembly mod)
    {
        var coordinator = mod.GetType("dda.RunCoordinator");
        var active = coordinator.GetField("Active", Flags); var running = coordinator.GetField("InRun", Flags);
        var characterType = AccessTools.TypeByName("Character"); var local = characterType.GetField("localCharacter", Flags);
        var runType = AccessTools.TypeByName("RunManager"); var runInstance = runType.GetField("Instance", Flags);
        FieldInfo[] savedFields = { active, running, local, runInstance };
        object[] savedValues = new object[savedFields.Length];
        for (int i = 0; i < savedFields.Length; i++) savedValues[i] = savedFields[i].GetValue(null);
        var patches = new Harmony("dda.continued.tornado-recovery-fixture");
        var root = new GameObject("Continued tornado recovery fixture"); root.SetActive(false);
        var hipRoot = new GameObject("Hip"); hipRoot.transform.SetParent(root.transform);
        var itemRoot = new GameObject("Recovery item"); itemRoot.SetActive(false);
        var otherRoot = new GameObject("Other recovery item"); otherRoot.SetActive(false);
        var recovery = mod.GetType("dda.TornadoRecovery");
        var tick = recovery.GetMethod("Tick", Flags); var reset = recovery.GetMethod("Reset", Flags);
        var observe = recovery.GetMethod("ObserveCapture", Flags);
        int checks = 0;
        try
        {
            var viewType = AccessTools.TypeByName("Photon.Pun.PhotonView"); root.AddComponent(viewType);
            character = root.AddComponent(characterType); local.SetValue(null, character);
            var dataType = AccessTools.TypeByName("CharacterData"); data = root.AddComponent(dataType);
            Set(character, "data", data); Set(data, "character", character);
            var refsField = characterType.GetField("refs", Flags); var references = Activator.CreateInstance(refsField.FieldType);
            refsField.SetValue(character, references);
            var afflictionsType = AccessTools.TypeByName("CharacterAfflictions");
            var afflictions = root.AddComponent(afflictionsType); Set(afflictions, "character", character);
            afflictionsType.GetMethod("InitStatusArrays", Flags).Invoke(afflictions, null); Set(references, "afflictions", afflictions);
            var movementType = AccessTools.TypeByName("CharacterMovement"); var movement = root.AddComponent(movementType);
            Set(movement, "character", character); Set(references, "movement", movement); Set(movement, "fallDamageTime", 1.25f);
            var itemsType = AccessTools.TypeByName("CharacterItems"); var items = root.AddComponent(itemsType);
            Set(items, "character", character); Set(references, "items", items);
            var animationsType = AccessTools.TypeByName("CharacterAnimations"); var animations = root.AddComponent(animationsType);
            Set(animations, "character", character); Set(references, "animations", animations);
            var ragdollType = AccessTools.TypeByName("CharacterRagdoll"); var ragdoll = root.AddComponent(ragdollType);
            Set(ragdoll, "character", character); Set(references, "ragdoll", ragdoll);
            var bodyType = AccessTools.TypeByName("Bodypart"); var rigid = hipRoot.AddComponent<Rigidbody>(); rigid.useGravity = false;
            var body = hipRoot.AddComponent(bodyType); Set(body, "character", character); Set(body, "rig", rigid); Set(body, "started", true);
            Set(references, "hip", body); ((IList)Get(ragdoll, "partList")).Add(body);
            var itemType = AccessTools.TypeByName("Item"); var item = itemRoot.AddComponent(itemType); var otherItem = otherRoot.AddComponent(itemType);
            var tornadoType = AccessTools.TypeByName("Tornado"); var tornado = root.AddComponent(tornadoType);
            var caught = (IList)Get(tornado, "caughtCharacters"); var ignored = (IList)Get(tornado, "ignoredCharacters");
            var run = root.AddComponent(runType); runInstance.SetValue(null, run);
            var runId = runType.GetProperty("RunId", Flags); var id = Guid.NewGuid(); runId.SetValue(run, id);

            // Keep native play-dead/lock, body alignment, velocity sampling and damage
            // calculation. Substitute RPC delivery and the equipment spawn/coroutine only.
            patches.Patch(characterType.GetMethod("Fall", Flags), prefix: new HarmonyMethod(typeof(TornadoRecoveryRegression), "Fall"));
            var emote = animationsType.GetMethod("RPCA_PlayRemove", Flags);
            patches.Patch(emote, prefix: new HarmonyMethod(typeof(TornadoRecoveryRegression), "Emote") { priority = Priority.Last });
            var equip = itemsType.GetMethod("Equip", Flags);
            patches.Patch(equip, prefix: new HarmonyMethod(typeof(TornadoRecoveryRegression), "Equip") { priority = Priority.Last });
            var addStatus = afflictionsType.GetMethod("AddStatus", Flags);
            patches.Patch(addStatus, prefix: new HarmonyMethod(typeof(TornadoRecoveryRegression), "Status") { priority = Priority.Last });
            var ascents = AccessTools.TypeByName("Ascents");
            patches.Patch(AccessTools.PropertyGetter(ascents, "fallDamageMultiplier"), prefix: new HarmonyMethod(typeof(TornadoRecoveryRegression), "Multiplier"));
            patches.Patch(AccessTools.PropertyGetter(ascents, "etcDamageMultiplier"), prefix: new HarmonyMethod(typeof(TornadoRecoveryRegression), "Multiplier"));
            var land = characterType.GetMethod("OnLand", Flags);
            patches.Patch(land, prefix: new HarmonyMethod(typeof(TornadoRecoveryRegression), "SkipLandingFeedback"));
            var snap = bodyType.GetMethod("SnapToAnim", Flags); var checkFall = movementType.GetMethod("CheckFallDamage", Flags);
            var sample = movementType.GetMethod("UpdateVariables", Flags); var lockSwitch = itemsType.GetMethod("LockFromSwitching", Flags);
            running.SetValue(null, true); SetRules(20); Set(data, "isGrounded", false);
            Set(data, "sinceGrounded", 10f); Set(data, "sinceJump", 10f); Set(data, "lastBouncedTime", -10000f);

            // Ordinary falling cannot gain the technique just by playing dead or equipping.
            reset.Invoke(null, null); Set(items, "lastEquippedSlotTime", -10f);
            PlayDead(); Assert((float)Get(items, "lastEquippedSlotTime") >= Time.time + 2.9f, "ordinary play-dead keeps native three-second lock");
            Equip(item); Assert(SnapVelocity(-30) == -30, "ordinary fall retains no-cancel patch");
            CheckDamage(); Assert(injuryCalls == 1 && injury > 0, "ordinary high-speed fall still reaches native injury gate");
            Assert(fallCalls > 0, "high-speed fall still triggers native ragdoll response");

            // Only an actual entry in this tornado's caught list qualifies.
            reset.Invoke(null, null); observe.Invoke(null, new object[] { tornado }); PlayDead(); Equip(item);
            Assert(SnapVelocity(-30) == -30, "nearby but uncaptured player cannot use exception");
            caught.Add(character); ignored.Add(character); observe.Invoke(null, new object[] { tornado }); PlayDead(); Equip(item);
            Assert(SnapVelocity(-30) == -30, "ignored player cannot manufacture a new capture"); ignored.Clear();
            reset.Invoke(null, null); Set(data, "isGrounded", true); observe.Invoke(null, new object[] { tornado });
            caught.Clear(); Set(data, "isGrounded", false); tick.Invoke(null, null); PlayDead(); Equip(item);
            Assert(SnapVelocity(-30) == -30, "capture that never lifts player cannot qualify a later cliff fall");
            caught.Add(character); Set(data, "isGrounded", true); observe.Invoke(null, new object[] { tornado });
            Set(data, "isGrounded", false); observe.Invoke(null, new object[] { tornado }); PlayDead(); Equip(item);
            Assert(SnapVelocity(-30) == 0, "first airborne capture tick enables recovery after grounded capture");
            reset.Invoke(null, null); observe.Invoke(null, new object[] { tornado }); Equip(item);
            Assert(SnapVelocity(-30) == -30, "capture plus item switch without play-dead does not qualify");
            emote.Invoke(animations, new object[] { "A_Scout_Emote_Wave", false }); Equip(item);
            Assert(SnapVelocity(-30) == -30, "other emotes do not qualify");
            PlayDead(); Assert(SnapVelocity(-30) == -30, "play-dead alone does not clear velocity");

            Set(items, "lastEquippedSlotTime", -10f); fallCalls = 0; PlayDead();
            Assert(fallCalls == 1 && fallDuration == 3f, "qualifying play-dead keeps native ragdoll action");
            Assert((float)Get(items, "lastEquippedSlotTime") == -10f, "only the play-dead switch delay is omitted");
            Assert(recovery.GetField("PlayDeadScope", Flags).GetValue(null) == null, "play-dead scope restored on return");
            lockSwitch.Invoke(items, new object[] { 8f });
            float existingLock = (float)Get(items, "lastEquippedSlotTime"); PlayDead();
            Assert((float)Get(items, "lastEquippedSlotTime") == existingLock, "pre-existing unrelated lock is retained");
            Set(items, "lastEquippedSlotTime", -10f);
            Equip(item); Assert(SnapVelocity(-30) == 0, "native body alignment clears downward velocity after valid combo");
            CheckDamage(); Assert(injuryCalls == 0, "native damage gate accepts sufficiently slowed landing");
            Assert(!(bool)Get(data, "isInvincible") && (float)Get(data, "sinceGrounded") == 10f, "no invincibility or fake grounded time is granted");
            // Gravity can build up again: this is cancellation at the switch, not guaranteed survival.
            rigid.linearVelocity = new Vector3(0, -30, 0); CheckDamage();
            Assert(injuryCalls == 1, "accelerating again after the switch still incurs ordinary damage");
            dataType.GetProperty("currentItem", Flags).SetValue(data, otherItem);
            Assert(SnapVelocity(-30) == -30, "different held item cannot reuse the previous alignment allowance");
            Equip(item); recovery.GetField("equipUntil", Flags).SetValue(null, Time.fixedTime - 1f);
            Assert(SnapVelocity(-30) == -30, "allowance expires after equipment alignment frames");
            Equip(null); Assert(SnapVelocity(-30) == -30, "empty hand does not qualify as item equipment");

            foreach (int level in new[] { 13, 14, 19, 20, -13 })
            {
                SetRules(level); reset.Invoke(null, null); observe.Invoke(null, new object[] { tornado }); PlayDead(); Equip(item);
                Assert(SnapVelocity(-30) == 0, "valid tornado recovery at selected/forced tier " + level);
                land.Invoke(character, new object[] { 10f }); Equip(item);
                Assert(SnapVelocity(-30) == -30, "landing clears qualification before the next ordinary fall");
            }
            foreach (string flag in new[] { "isGrounded", "isClimbing", "isRopeClimbing", "isVineClimbing", "passedOut", "fullyPassedOut" })
            {
                SetRules(20); reset.Invoke(null, null); observe.Invoke(null, new object[] { tornado }); PlayDead(); Equip(item);
                Set(data, flag, true); tick.Invoke(null, null); Set(data, flag, false);
                Assert(SnapVelocity(-30) == -30, "support/incapacitation clears qualification: " + flag);
            }
            Qualify(); dataType.GetProperty("dead", Flags).SetValue(data, true); tick.Invoke(null, null);
            dataType.GetProperty("dead", Flags).SetValue(data, false);
            Assert(SnapVelocity(-30) == -30, "death clears recovery before revival");
            Qualify(); characterType.GetProperty("warping", Flags).SetValue(character, true); tick.Invoke(null, null);
            characterType.GetProperty("warping", Flags).SetValue(character, false);
            Assert(SnapVelocity(-30) == -30, "warp clears qualification");
            Qualify(); runId.SetValue(run, Guid.NewGuid()); tick.Invoke(null, null); runId.SetValue(run, id);
            Assert(SnapVelocity(-30) == -30, "qualification does not cross RunId");
            Qualify(); local.SetValue(null, null); tick.Invoke(null, null); local.SetValue(null, character);
            Assert(SnapVelocity(-30) == -30, "local character replacement clears qualification");
            Qualify(); recovery.GetMethod("Forget", Flags).Invoke(null, new[] { character });
            Assert(SnapVelocity(-30) == -30, "explicit revive/landing cleanup clears qualification");
            Qualify(); reset.Invoke(null, null); Assert(SnapVelocity(-30) == -30, "run reset clears qualification");

            reset.Invoke(null, null); observe.Invoke(null, new object[] { tornado }); failEmote = true;
            AssertThrows(PlayDead); failEmote = false;
            Assert(recovery.GetField("PlayDeadScope", Flags).GetValue(null) == null, "exception restores play-dead scope");
            Equip(item); Assert(SnapVelocity(-30) == -30, "failed emote cannot unlock recovery");
            PlayDead(); failEquip = true; AssertThrows(() => Equip(item)); failEquip = false;
            Assert(SnapVelocity(-30) == -30, "failed equipment cannot leave alignment allowance");

            foreach (int level in new[] { 0, 12 })
            {
                SetRules(level); reset.Invoke(null, null); observe.Invoke(null, new object[] { tornado });
                Set(items, "lastEquippedSlotTime", -10f); PlayDead(); Equip(item);
                Assert((float)Get(items, "lastEquippedSlotTime") >= Time.time + 2.9f && SnapVelocity(-30) == 0,
                    "official/lower tiers keep native lock and native body alignment");
            }
            return checks;

            void PlayDead() => emote.Invoke(animations, new object[] { "A_Scout_Emote_PlayDead", false });
            void Equip(object value) => equip.Invoke(items, new[] { value });
            void Qualify() { SetRules(20); reset.Invoke(null, null); observe.Invoke(null, new object[] { tornado }); PlayDead(); Equip(item); }
            float SnapVelocity(float speed) { rigid.linearVelocity = new Vector3(0, speed, 0); snap.Invoke(body, null); return rigid.linearVelocity.y; }
            void CheckDamage() { injuryCalls = 0; injury = 0; sample.Invoke(movement, null); sample.Invoke(movement, null); checkFall.Invoke(movement, null); }
            void AssertThrows(Action action) { try { action(); } catch (TargetInvocationException e) when(e.InnerException is InvalidOperationException) { checks++; return; } throw new Exception("Expected fixture exception"); }
        }
        finally
        {
            reset.Invoke(null, null); failEmote = failEquip = false; patches.UnpatchSelf();
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(itemRoot); UnityEngine.Object.DestroyImmediate(otherRoot);
            for (int i = 0; i < savedFields.Length; i++) savedFields[i].SetValue(null, savedValues[i]);
            character = data = null;
        }
        void SetRules(int level) => active.SetValue(null, Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"), Math.Max(0, level), level < 0 ? 1 << 4 : 0, true, true));
        void Assert(bool pass, string reason) { if (!pass) throw new Exception(reason); checks++; }
    }
    private static object Get(object target, string name) => AccessTools.Field(target.GetType(), name).GetValue(target);
    private static void Set(object target, string name, object value) => AccessTools.Field(target.GetType(), name).SetValue(target, value);
    private static bool Fall(float seconds) { if (failEmote) throw new InvalidOperationException("Expected emote fixture exception"); fallCalls++; fallDuration = seconds; return false; }
    private static bool Emote(string emoteName) => emoteName == "A_Scout_Emote_PlayDead";
    private static bool Equip(object item, ref object __result)
    {
        if (failEquip) throw new InvalidOperationException("Expected equipment fixture exception");
        data.GetType().GetProperty("currentItem", Flags).SetValue(data, item); __result = null; return false;
    }
    private static bool Status(object statusType, float amount, ref bool __result)
    { if (statusType.ToString() == "Injury") { injuryCalls++; injury = amount; } __result = false; return false; }
    private static bool Multiplier(ref float __result) { __result = 1f; return false; }
    private static bool SkipLandingFeedback() => false;
}
