// Adapted from the user-supplied 13dda.dll. Original design: pooblives.
using HarmonyLib;
using UnityEngine;

namespace dda;

[HarmonyPatch(typeof(MapHandler), "InitializeMap")]
internal static class allWind
{
    public static void Postfix() => EveryMapSnow.Tick();
}
