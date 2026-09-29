using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

internal static class ClientFallRegression
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static object character;
    private static float injury, drowsy;
    private static int drowsyCalls;
    private static bool accept;

    // Called while ClientRulesRegression is a non-host that adopted room rules.
    // Native fall calculation and all Continued patches run; status delivery/HUD
    // and achievements are substituted. Native AddStatus is tested separately.
    internal static int Run(Assembly mod)
    {
        var type = AccessTools.TypeByName("Character"); var local = type.GetField("localCharacter", F);
        object oldLocal = local.GetValue(null);
        var patches = new Harmony("dda.continued.client-fall-fixture");
        var root = new GameObject("Non-host fall fixture"); root.SetActive(false);
        int checks = 0;
        try
        {
            var view = root.AddComponent<Photon.Pun.PhotonView>();
            Set(view, "<IsMine>k__BackingField", true);
            character = root.AddComponent(type); local.SetValue(null, character);
            Set(character, "view", view);
            var data = root.AddComponent(AccessTools.TypeByName("CharacterData")); Set(character, "data", data);
            Set(data, "character", character); Set(data, "isInFog", true);
            var refs = Activator.CreateInstance(type.GetField("refs", F).FieldType); Set(character, "refs", refs);
            var affType = AccessTools.TypeByName("CharacterAfflictions"); var aff = root.AddComponent(affType);
            Set(aff, "character", character); Set(refs, "afflictions", aff);
            affType.GetMethod("InitStatusArrays", F).Invoke(aff, null);
            var movement = root.AddComponent(AccessTools.TypeByName("CharacterMovement")); Set(movement, "character", character);
            Set(movement, "fallDamageTime", 1.25f);
            // The native fall cap keeps injury below the achievement/UI threshold,
            // while still exercising the real >0 fall calculation and its AddStatus.
            Set(movement, "fallDamageCapEndTime", Time.time + 100f); Set(movement, "fallDamageCap", .01f);
            Set(data, "sinceGrounded", 2f); Set(data, "sinceJump", 2f); Set(data, "lastBouncedTime", -10000f);
            Set(data, "avarageLastFrameVelocity", Vector3.down * 15);
            patches.Patch(affType.GetMethod("AddStatus", F), prefix: new HarmonyMethod(typeof(ClientFallRegression), nameof(Status)) { priority = Priority.Last });
            patches.Patch(AccessTools.PropertyGetter(AccessTools.TypeByName("Ascents"), "etcDamageMultiplier"), prefix: new HarmonyMethod(typeof(ClientFallRegression), nameof(Unit)));
            patches.Patch(AccessTools.PropertyGetter(AccessTools.TypeByName("Ascents"), "fallDamageMultiplier"), prefix: new HarmonyMethod(typeof(ClientFallRegression), nameof(Unit)));
            var fall = movement.GetType().GetMethod("CheckFallDamage", F);
            accept = true; Reset(); fall.Invoke(movement, null);
            Assert(injury > 0 && drowsyCalls == 1 && drowsy > 0, "non-host native fall triggers exactly one drowsiness penalty");
            Assert(Math.Abs(drowsy - (1.04f - injury) * 1.3f) < .0001f, "client preserves tier 13 threshold and tier 17 multiplier");
            accept = false; Reset(); Set(movement, "fallDamageCapEndTime", Time.time + 100f); fall.Invoke(movement, null);
            Assert(drowsyCalls == 0, "rejected injury does not trigger sleep");
            accept = true; local.SetValue(null, null); Reset(); Set(movement, "fallDamageCapEndTime", Time.time + 100f); fall.Invoke(movement, null);
            Assert(drowsyCalls == 0, "remote replica never adds another player's sleep penalty");
            local.SetValue(null, character); Reset(); Set(data, "sinceGrounded", 0f); fall.Invoke(movement, null);
            Assert(injury == 0 && drowsyCalls == 0, "safe landing adds no penalty");
            patches.Unpatch(affType.GetMethod("AddStatus", F), HarmonyPatchType.Prefix, patches.Id);
            foreach (var status in new[] { ("Cold", 1.3f), ("Drowsy", 1.3f), ("Poison", 1.75f) })
            {
                var kind = Enum.Parse(affType.GetNestedType("STATUSTYPE"), status.Item1);
                bool accepted = (bool)affType.GetMethod("AddStatus", F).Invoke(aff,
                    new object[] { kind, .001f, false, false, false, false, false });
                float increment = ((float[])Get(aff, "currentIncrementalStatuses"))[Convert.ToInt32(kind)];
                Assert(accepted && Math.Abs(increment - .001f * status.Item2) < 1e-7f,
                    "non-host native status ownership and multiplier: " + status.Item1);
            }
            return checks;
        }
        finally { patches.UnpatchSelf(); local.SetValue(null, oldLocal); UnityEngine.Object.DestroyImmediate(root); character = null; }
        void Assert(bool pass, string reason) { if (!pass) throw new Exception(reason); checks++; }
    }
    private static void Reset()
    {
        injury = drowsy = 0; drowsyCalls = 0;
        var refs = Get(character, "refs"); var aff = Get(refs, "afflictions");
        Array.Clear((float[])Get(aff, "currentStatuses"), 0, 15);
    }
    private static bool Status(object __instance, object statusType, float amount, ref bool __result)
    {
        __result = accept;
        if (!accept) return false;
        if (statusType.ToString() == "Injury") injury = amount;
        if (statusType.ToString() == "Drowsy") { drowsy = amount; drowsyCalls++; }
        ((float[])Get(__instance, "currentStatuses"))[Convert.ToInt32(statusType)] += amount;
        return false;
    }
    private static bool Unit(ref float __result) { __result = 1; return false; }
    private static object Get(object target, string name) => AccessTools.Field(target.GetType(), name).GetValue(target);
    private static void Set(object target, string name, object value) => AccessTools.Field(target.GetType(), name).SetValue(target, value);
}
