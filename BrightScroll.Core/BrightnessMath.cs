namespace BrightScroll.Core;

public static class BrightnessMath
{
    public static int Adjust(int current, int increment, int wheelNotches)
    {
        if (current is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(current));
        }

        if (increment is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(increment));
        }

        return (int)Math.Clamp((long)current + (long)increment * wheelNotches, 0, 100);
    }
}
