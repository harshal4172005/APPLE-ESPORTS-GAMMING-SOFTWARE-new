namespace AppleEsportsErp.Application.Interfaces;

public interface ITokenRevocationService
{
    Task RevokeTokenAsync(string jti, TimeSpan expiration);
    Task RevokeUserTokensAsync(Guid userId, TimeSpan expiration);
    Task<bool> IsTokenRevokedAsync(string jti, Guid userId, DateTimeOffset tokenIssueTime);
}
