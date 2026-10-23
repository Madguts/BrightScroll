namespace BrightScroll.Core;

internal static class TrayIconRetryDelay
{
    internal static TimeSpan GetDelay(int consecutiveFailures)
    {
        if (consecutiveFailures < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(consecutiveFailures));
        }

        var exponent = Math.Min(consecutiveFailures - 1, 4);
        return TimeSpan.FromSeconds(Math.Min(5 * (1 << exponent), 60));
    }
}
