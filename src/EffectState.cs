using System.Collections.Generic;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace dda;

internal static class EffectState
{
    private static ConditionalWeakTable<UnityEngine.Object, Dictionary<string, float>> baselines = new();
    private sealed class Change
    {
        internal WeakReference<object> owner;
        internal MemberInfo member;
        internal object original, applied;
        internal object Read(object target) => member is FieldInfo field ? field.GetValue(target) : ((PropertyInfo)member).GetValue(target, null);
        internal void Write(object target, object value)
        {
            if (member is FieldInfo field) field.SetValue(target, value);
            else ((PropertyInfo)member).SetValue(target, value, null);
        }
    }
    private static readonly Dictionary<string, MemberInfo> members = new();
    private static ConditionalWeakTable<object, Dictionary<string, Change>> tracked = new();
    private static readonly List<Change> changes = new();

    // Capture design parameters once per instance and restore them at the airport. This also
    // handles game objects that survive a scene switch; elapsed timers/positions are not rewound.
    internal static void Set(object owner, string name, object value)
    {
        var values = tracked.GetOrCreateValue(owner);
        if (!values.TryGetValue(name, out var change))
        {
            string key = owner.GetType().FullName + ":" + name;
            if (!members.TryGetValue(key, out var member))
            {
                var type = owner.GetType();
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                while (type != null)
                {
                    member = (MemberInfo)type.GetField(name, flags) ?? type.GetProperty(name, flags);
                    if (member != null) break;
                    type = type.BaseType;
                }
                if (member == null) throw new MissingMemberException(key);
                members[key] = member;
            }
            change = new Change { owner = new WeakReference<object>(owner), member = member };
            change.original = change.Read(owner);
            values[name] = change;
            changes.Add(change);
        }
        change.Write(owner, value);
        change.applied = value;
    }
    internal static float Original(UnityEngine.Object owner, string key, float current)
    {
        var values = baselines.GetOrCreateValue(owner);
        if (!values.TryGetValue(key, out float original)) values[key] = original = current;
        return original;
    }
    internal static void Reset()
    {
        ChasingFogTint.Reset();
        SwampChasingFog.Reset();
        foreach (var change in changes)
        {
            if (!change.owner.TryGetTarget(out var owner)) continue;
            if (change.member is PropertyInfo && owner is UnityEngine.Object unityObject && unityObject == null) continue;
            try
            {
                // Preserve a later intentional edit by another mod.
                if (Equals(change.Read(owner), change.applied)) change.Write(owner, change.original);
            }
            catch (Exception error) { Plugin.Log.LogWarning("Could not restore a Continued parameter: " + error.Message); }
        }
        changes.Clear();
        allWeather.Reset();
        tracked = new ConditionalWeakTable<object, Dictionary<string, Change>>();
        baselines = new ConditionalWeakTable<UnityEngine.Object, Dictionary<string, float>>();
    }
}
