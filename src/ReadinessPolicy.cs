namespace dda;

public enum PeerReadiness { Ready, VersionMismatch, RulesPending }

public static class ReadinessPolicy
{
    public static PeerReadiness Check(DifficultySnapshot selected, string requiredVersion, string peerVersion, string acknowledgement)
    {
        if (!selected.HasEffects) return PeerReadiness.Ready;
        if (peerVersion != requiredVersion) return PeerReadiness.VersionMismatch;
        return acknowledgement == selected.Encode() ? PeerReadiness.Ready : PeerReadiness.RulesPending;
    }
}
