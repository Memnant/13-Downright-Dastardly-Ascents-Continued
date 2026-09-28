using System.Runtime.CompilerServices;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace dda;

// Only the additional tornados use custom movement. Native Update owns lifetime, sync and cleanup.
[HarmonyPatch(typeof(Tornado), "Movement")]
internal static class customAI
{
    internal const string Marker = "dda.continued.tornado.v1";
    private sealed class Wander { internal Vector3 target; internal float remaining; }
    private static readonly ConditionalWeakTable<Tornado, Wander> States = new();
    internal static bool IsCustom(Tornado tornado)
    {
        var view = tornado.view != null ? tornado.view : tornado.GetComponent<PhotonView>();
        var data = view == null ? null : view.InstantiationData;
        return data != null && data.Length > 0 && data[0] as string == Marker;
    }
    private static bool Prefix(Tornado __instance)
    {
        if (!Rules.Enabled(20) || !IsCustom(__instance)) return true;
        __instance.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        var state = States.GetOrCreateValue(__instance);
        if (__instance.view.IsMine)
        {
            state.remaining -= Time.deltaTime;
            if (state.remaining <= 0f)
            {
                TornadoBoundary.TryTarget(__instance.tornadoPos, out state.target);
                state.remaining = 5f;
            }
            __instance.vel = FRILerp.Lerp(__instance.vel, (state.target - __instance.tornadoPos).Flat().normalized * 15f, 0.15f);
        }
        __instance.tornadoPos = TornadoBoundary.Step(__instance.tornadoPos,
            __instance.tornadoPos + __instance.vel * Time.deltaTime * 0.5f, Time.deltaTime, out bool blocked);
        if (blocked) { state.remaining = 0f; __instance.vel = Vector3.zero; }
        __instance.transform.position = __instance.tornadoPos;
        return false;
    }
}

[HarmonyPatch(typeof(Tornado), "TargetSelection")]
internal static class fixItFelix
{
    private static bool Prefix(Tornado __instance) => !Rules.Enabled(20) || !customAI.IsCustom(__instance);
}

[HarmonyPatch(typeof(Tornado), "Update")]
internal static class ContinuedFinalSegmentTornado
{
    private static bool Prefix(Tornado __instance)
    {
        if (!EveryMapSnow.Excluded || !customAI.IsCustom(__instance)) return true;
        var view = __instance.view != null ? __instance.view : __instance.GetComponent<PhotonView>();
        if (view != null && view.IsMine) PhotonNetwork.Destroy(__instance.gameObject);
        else __instance.gameObject.SetActive(false); // Hide locally while the owning peer sends destruction.
        return false;
    }
}

[HarmonyPatch(typeof(Tornado), "FixedUpdate")]
internal static class ContinuedFinalSegmentTornadoPhysics
{
    private static bool Prefix(Tornado __instance) => !EveryMapSnow.Excluded || !customAI.IsCustom(__instance);
}
