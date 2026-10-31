namespace DOOM.Core.State;

public enum PrivacyVerificationStatus
{
    NotTested,
    SelfTestPassed,
    SelfTestFailed,
    Unavailable
}

public enum SharingContext
{
    NotSure,
    EntireScreen,
    SingleWindow,
    SeparateDisplay
}

public enum ProtectionStatus
{
    NotRequested,
    RequestedUnverified,
    SelfTestPassed,
    SelfTestFailed,
    KnownNotEffective,
    Unavailable
}

/// <summary>
/// Tracks the complete separation between requested user setting,
/// applied native display-affinity state, and independently tested capture behavior.
/// </summary>
public sealed record PrivacyProtectionState
{
    public bool IsRequested { get; init; }
    public bool IsApplied { get; init; }
    public PrivacyVerificationStatus Verification { get; init; }
    public SharingContext Context { get; init; }
    public string? StatusReason { get; init; }
    public DateTimeOffset? LastVerificationTime { get; init; }

    public static readonly PrivacyProtectionState Default = new()
    {
        IsRequested = false,
        IsApplied = false,
        Verification = PrivacyVerificationStatus.NotTested,
        Context = SharingContext.NotSure
    };

    /// <summary>
    /// Computes the honest effective protection status based on all three independent parameters.
    /// </summary>
    public ProtectionStatus EffectiveStatus
    {
        get
        {
            if (!IsRequested)
            {
                return ProtectionStatus.NotRequested;
            }

            if (!IsApplied)
            {
                return ProtectionStatus.Unavailable;
            }

            if (Context == SharingContext.EntireScreen)
            {
                // Full screen sharing in certain meeting tools can capture desktop buffers directly
                return ProtectionStatus.KnownNotEffective;
            }

            return Verification switch
            {
                PrivacyVerificationStatus.SelfTestPassed => ProtectionStatus.SelfTestPassed,
                PrivacyVerificationStatus.SelfTestFailed => ProtectionStatus.SelfTestFailed,
                PrivacyVerificationStatus.Unavailable => ProtectionStatus.Unavailable,
                _ => ProtectionStatus.RequestedUnverified
            };
        }
    }

    /// <summary>
    /// Returns true only if requested AND successfully applied by the OS.
    /// </summary>
    public bool IsProtectionActive => IsRequested && IsApplied;
}

