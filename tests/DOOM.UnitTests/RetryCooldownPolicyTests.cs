namespace DOOM.UnitTests;

using DOOM.Core.Domain;
using DOOM.Core.Errors;

public class RetryCooldownPolicyTests
{
    [Fact]
    public void ComputeCooldown_FirstFailure_ReturnsZero()
    {
        var cooldown = RetryCooldownPolicy.ComputeCooldown(0);
        Assert.Equal(TimeSpan.Zero, cooldown);
    }

    [Fact]
    public void ComputeCooldown_HigherFailures_ScalesExponentially()
    {
        // Without jitter, base schedule is: [0, 2, 4, 8, 16, 30]
        var cooldown1 = RetryCooldownPolicy.ComputeCooldown(1, explicitJitter: 1.0);
        var cooldown2 = RetryCooldownPolicy.ComputeCooldown(2, explicitJitter: 1.0);
        var cooldown3 = RetryCooldownPolicy.ComputeCooldown(3, explicitJitter: 1.0);
        var cooldown4 = RetryCooldownPolicy.ComputeCooldown(4, explicitJitter: 1.0);
        var cooldown5 = RetryCooldownPolicy.ComputeCooldown(5, explicitJitter: 1.0);
        var cooldownMax = RetryCooldownPolicy.ComputeCooldown(10, explicitJitter: 1.0);

        Assert.Equal(TimeSpan.FromSeconds(2), cooldown1);
        Assert.Equal(TimeSpan.FromSeconds(4), cooldown2);
        Assert.Equal(TimeSpan.FromSeconds(8), cooldown3);
        Assert.Equal(TimeSpan.FromSeconds(16), cooldown4);
        Assert.Equal(TimeSpan.FromSeconds(30), cooldown5);
        Assert.Equal(TimeSpan.FromSeconds(30), cooldownMax); // Capped at 30s
    }

    [Fact]
    public void GetCooldownForError_RateLimitedWithRetryAfter_ClampsTo1And60Seconds()
    {
        var errShort = new GenerationException(GenerationError.RateLimited, "Rate limited", retryAfter: TimeSpan.FromSeconds(0.5));
        var errLong = new GenerationException(GenerationError.RateLimited, "Rate limited", retryAfter: TimeSpan.FromSeconds(120));
        var errNormal = new GenerationException(GenerationError.RateLimited, "Rate limited", retryAfter: TimeSpan.FromSeconds(25));
        var errNoHeader = new GenerationException(GenerationError.RateLimited, "Rate limited");

        Assert.Equal(TimeSpan.FromSeconds(1), RetryCooldownPolicy.GetCooldownForError(errShort, 1));
        Assert.Equal(TimeSpan.FromSeconds(60), RetryCooldownPolicy.GetCooldownForError(errLong, 1));
        Assert.Equal(TimeSpan.FromSeconds(25), RetryCooldownPolicy.GetCooldownForError(errNormal, 1));
        Assert.Equal(TimeSpan.FromSeconds(10), RetryCooldownPolicy.GetCooldownForError(errNoHeader, 1));
    }

    [Fact]
    public void GetCooldownForError_NonRetryableError_ReturnsNull()
    {
        var errAuth = new GenerationException(GenerationError.InvalidApiKey, "Bad API Key");
        Assert.Null(RetryCooldownPolicy.GetCooldownForError(errAuth, 1));
    }

    [Fact]
    public void IsAllowed_TracksCooldownWindowCorrectly()
    {
        var policy = new RetryCooldownPolicy();
        var t0 = DateTimeOffset.UtcNow;

        Assert.True(policy.IsAllowed(t0));

        // Record a 5-second cooldown
        policy.RecordAttempt(TimeSpan.FromSeconds(5), t0);

        // Before 5 seconds elapsed
        Assert.False(policy.IsAllowed(t0.AddSeconds(2)));
        Assert.Equal(TimeSpan.FromSeconds(3), policy.GetRemainingCooldown(t0.AddSeconds(2)));

        // After 5 seconds elapsed
        Assert.True(policy.IsAllowed(t0.AddSeconds(6)));
        Assert.Equal(TimeSpan.Zero, policy.GetRemainingCooldown(t0.AddSeconds(6)));
    }
}

