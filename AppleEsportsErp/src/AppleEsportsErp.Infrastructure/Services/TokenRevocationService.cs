using Microsoft.Extensions.Caching.Memory;
using AppleEsportsErp.Application.Interfaces;

namespace AppleEsportsErp.Infrastructure.Services;

public class TokenRevocationService : ITokenRevocationService
{
    private readonly IMemoryCache _cache;

    public TokenRevocationService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task RevokeTokenAsync(string jti, TimeSpan expiration)
    {
        _cache.Set($"revoked_jti_{jti}", true, expiration);
        return Task.CompletedTask;
    }

    public Task RevokeUserTokensAsync(Guid userId, TimeSpan expiration)
    {
        // For forced logout, we revoke all tokens issued to the user before this moment
        _cache.Set($"revoked_user_{userId}", DateTimeOffset.UtcNow, expiration);
        return Task.CompletedTask;
    }

    public Task<bool> IsTokenRevokedAsync(string jti, Guid userId, DateTimeOffset tokenIssueTime)
    {
        if (_cache.TryGetValue($"revoked_jti_{jti}", out _))
        {
            return Task.FromResult(true);
        }

        if (_cache.TryGetValue($"revoked_user_{userId}", out DateTimeOffset revokedAt))
        {
            // If the token was issued BEFORE the revocation event, it is revoked.
            // If it was issued AFTER, it is a valid new session.
            if (tokenIssueTime <= revokedAt)
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }
}
