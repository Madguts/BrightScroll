using System.Security.Cryptography;
using System.Text;

namespace BrightScroll.Core;

internal static class NotificationIconIdentity
{
    internal static Guid Create(string displayIdentity)
    {
        if (string.IsNullOrWhiteSpace(displayIdentity))
        {
            throw new ArgumentException("A stable display identity is required.", nameof(displayIdentity));
        }

        var identity = $"Madguts.BrightScroll.NotificationIcon\0{displayIdentity.Trim().ToUpperInvariant()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return new Guid(hash.AsSpan(0, 16));
    }
}
