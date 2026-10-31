namespace DOOM.Core.Domain;

using DOOM.Core.Errors;

/// <summary>
/// Implements exponential backoff with jitter and server-directed rate-limit handling.
/// Faithful to the original macOS DOOM retry specification.
/// </summary>
public sealed class RetryCooldownPolicy
{
    private static readonly int[] BaseBackoffSchedule = [0, 2, 4, 8, 16, 30];
    private static readonly Random RandomSource = new();

    private readonly object _lock = new();
    private DateTimeOffset? _lastAttempt;
    private TimeSpan _activeCooldown = TimeSpan.Zero;

    public TimeSpan CooldownDuration => _activeCooldown;
    public DateTimeOffset? LastAttempt => _lastAttempt;

    public RetryCooldownPolicy()
    {
    }

    /// <summary>
    /// Computes the exponential cooldown with jitter for a given failure count.
    /// Schedule: 0s (1st), 2s (2nd), 4s (3rd), 8s (4th), 16s (5th), 30s cap.
    /// Jitter: [0.8, 1.2]
    /// </summary>
    public static TimeSpan ComputeCooldown(int failureCount, double? explicitJitter = null)
    {
        var clampedIndex = Math.Clamp(failureCount, 0, BaseBackoffSchedule.Length - 1);
        var baseSeconds = BaseBackoffSchedule[clampedIndex];

        if (baseSeconds == 0)
        {
            return TimeSpan.Zero;
        }

        var jitter = explicitJitter ?? (0.8 + (RandomSource.NextDouble() * 0.4));
        var finalSeconds = Math.Max(1, (int)Math.Round(baseSeconds * jitter));
        return TimeSpan.FromSeconds(finalSeconds);
    }

    /// <summary>
    /// Computes the cooldown for a specific generation error.
    /// Rate-limited errors respect server Retry-After (clamped to 1-60s, default 10s).
    /// </summary>
    public static TimeSpan? GetCooldownForError(GenerationException error, int failureCount)
    {
        if (error.ErrorCode == GenerationError.RateLimited)
        {
            if (error.RetryAfter.HasValue)
            {
                var clampedSeconds = Math.Clamp((int)error.RetryAfter.Value.TotalSeconds, 1, 60);
                return TimeSpan.FromSeconds(clampedSeconds);
            }

            return TimeSpan.FromSeconds(10);
        }

        return error.ErrorCode switch
        {
            GenerationError.Server or
            GenerationError.TimedOut or
            GenerationError.Transport or
            GenerationError.Offline or
            GenerationError.EmptyResponse or
            GenerationError.MalformedResponse => ComputeCooldown(failureCount),
            _ => null // Non-retryable
        };
    }

    /// <summary>
    /// Records an attempt with an explicit cooldown duration.
    /// </summary>
    public void RecordAttempt(TimeSpan cooldown, DateTimeOffset? now = null)
    {
        lock (_lock)
        {
            _lastAttempt = now ?? DateTimeOffset.UtcNow;
            _activeCooldown = cooldown;
        }
    }

    /// <summary>
    /// Checks whether the cooldown window has elapsed.
    /// </summary>
    public bool IsAllowed(DateTimeOffset? now = null)
    {
        lock (_lock)
        {
            if (!_lastAttempt.HasValue || _activeCooldown == TimeSpan.Zero)
            {
                return true;
            }

            var current = now ?? DateTimeOffset.UtcNow;
            return (current - _lastAttempt.Value) >= _activeCooldown;
        }
    }

    /// <summary>
    /// Returns remaining seconds until the cooldown expires.
    /// </summary>
    public TimeSpan GetRemainingCooldown(DateTimeOffset? now = null)
    {
        lock (_lock)
        {
            if (!_lastAttempt.HasValue || _activeCooldown == TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            var current = now ?? DateTimeOffset.UtcNow;
            var elapsed = current - _lastAttempt.Value;
            var remaining = _activeCooldown - elapsed;

            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }
}

