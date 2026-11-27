using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;

namespace SmartOps.Web.Services;

// Keep credentials in memory only; emails carry a random, short-lived reference.
public sealed class EmailLoginLinkService(IMemoryCache cache, TimeProvider time)
{
    private readonly object gate = new();
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    public string Create(string userId, string username, string password, string? securityStamp)
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var expires = time.GetUtcNow().Add(Lifetime);
        cache.Set(Key(token), new LoginDetails(userId, username, password, securityStamp, expires), Lifetime);
        return token;
    }

    public LoginDetails? Redeem(string? token)
    {
        if (token is null || token.Length != 43) return null;
        lock (gate)
        {
            var key = Key(token);
            if (!cache.TryGetValue<LoginDetails>(key, out var details)) return null;
            cache.Remove(key);
            return details?.ExpiresAt > time.GetUtcNow() ? details : null;
        }
    }

    public void Revoke(string token)
    {
        lock (gate) cache.Remove(Key(token));
    }

    private static string Key(string token) => "EmailLoginLink:" + token;
    public sealed record LoginDetails(string UserId, string Username, string Password,
        string? SecurityStamp, DateTimeOffset ExpiresAt);
}
