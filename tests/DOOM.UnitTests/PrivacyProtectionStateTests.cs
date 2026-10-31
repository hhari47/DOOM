namespace DOOM.UnitTests;

using DOOM.Core.State;

public class PrivacyProtectionStateTests
{
    [Fact]
    public void EffectiveStatus_WhenNotRequested_ReturnsNotRequested()
    {
        var state = new PrivacyProtectionState
        {
            IsRequested = false,
            IsApplied = true,
            Verification = PrivacyVerificationStatus.SelfTestPassed
        };

        Assert.Equal(ProtectionStatus.NotRequested, state.EffectiveStatus);
        Assert.False(state.IsProtectionActive);
    }

    [Fact]
    public void EffectiveStatus_WhenRequestedButNotApplied_ReturnsUnavailable()
    {
        var state = new PrivacyProtectionState
        {
            IsRequested = true,
            IsApplied = false,
            Verification = PrivacyVerificationStatus.NotTested
        };

        Assert.Equal(ProtectionStatus.Unavailable, state.EffectiveStatus);
        Assert.False(state.IsProtectionActive);
    }

    [Fact]
    public void EffectiveStatus_WhenEntireScreenShared_ReturnsKnownNotEffective()
    {
        var state = new PrivacyProtectionState
        {
            IsRequested = true,
            IsApplied = true,
            Context = SharingContext.EntireScreen,
            Verification = PrivacyVerificationStatus.SelfTestPassed
        };

        Assert.Equal(ProtectionStatus.KnownNotEffective, state.EffectiveStatus);
        Assert.True(state.IsProtectionActive); // Locally applied, but known ineffective against full desktop capture
    }

    [Fact]
    public void EffectiveStatus_WhenRequestedAndApplied_ReflectsVerificationState()
    {
        var unverified = new PrivacyProtectionState
        {
            IsRequested = true,
            IsApplied = true,
            Verification = PrivacyVerificationStatus.NotTested
        };

        var verified = new PrivacyProtectionState
        {
            IsRequested = true,
            IsApplied = true,
            Verification = PrivacyVerificationStatus.SelfTestPassed
        };

        var failed = new PrivacyProtectionState
        {
            IsRequested = true,
            IsApplied = true,
            Verification = PrivacyVerificationStatus.SelfTestFailed
        };

        Assert.Equal(ProtectionStatus.RequestedUnverified, unverified.EffectiveStatus);
        Assert.Equal(ProtectionStatus.SelfTestPassed, verified.EffectiveStatus);
        Assert.Equal(ProtectionStatus.SelfTestFailed, failed.EffectiveStatus);
    }
}

