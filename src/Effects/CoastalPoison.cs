using System;
using System.Runtime.CompilerServices;

namespace dda;

// Only a jellyfish or the native Urch/Collider contact gets this tier-17 exception.
internal static class CoastalPoison
{
    internal const float Amount = .4f;
    [ThreadStatic] private static CharacterAfflictions recipient;
    private sealed class HazardKind { internal bool Urchin; }
    private static readonly ConditionalWeakTable<CollisionModifier, HazardKind> kinds = new();

    internal static bool IsUrchin(CollisionModifier modifier)
    {
        if (!kinds.TryGetValue(modifier, out var kind))
        {
            kind = new HazardKind();
            for (var parent = modifier.transform.parent; parent != null; parent = parent.parent)
                if (parent.name == "Urch" || parent.name.StartsWith("Urch (", StringComparison.Ordinal))
                { kind.Urchin = true; break; }
            kinds.Add(modifier, kind);
        }
        return kind.Urchin;
    }

    internal static bool IsFixedFor(CharacterAfflictions afflictions) =>
        !ReferenceEquals(afflictions, null) && ReferenceEquals(recipient, afflictions);

    internal readonly struct Scope : IDisposable
    {
        private readonly CharacterAfflictions previous;
        internal Scope(CharacterAfflictions target) { previous = recipient; recipient = target; }
        public void Dispose() => recipient = previous;
    }

    internal readonly struct ContactState
    {
        internal readonly bool Active;
        private readonly float damage;
        private readonly Scope scope;
        internal ContactState(CollisionModifier modifier, Character character)
        {
            Active = true; damage = modifier.damage;
            scope = new Scope(character?.refs?.afflictions);
            modifier.damage = Amount;
        }
        internal void Restore(CollisionModifier modifier)
        {
            if (!Active) return;
            try { if (modifier != null && modifier.damage == Amount) modifier.damage = damage; }
            finally { scope.Dispose(); }
        }
    }

    internal static void ApplyJellyfish(Character character)
    {
        using var scope = new Scope(character.refs.afflictions);
        character.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Poison, Amount, true, true, true);
    }
}
