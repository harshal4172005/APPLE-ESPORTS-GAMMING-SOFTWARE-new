using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AppleEsportsErp.Infrastructure.Identity;

/// <summary>
/// JWT Token generation/validation service.
/// Maps from Node.js generateAccessToken/generateRefreshToken in auth.js.
/// Q1 Decision: Embed full claims (UserId, Role, BranchId, ShiftId, DashboardPermissions, DeviceId, SessionId)
/// </summary>
public class JwtTokenService
{
    private readonly string _secret;
    private readonly string _refreshSecret;
    private readonly string _accessExpiry;
    private readonly string _refreshExpiry;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtTokenService(string secret, string refreshSecret, string accessExpiry, string refreshExpiry, string issuer, string audience)
    {
        _secret = secret;
        _refreshSecret = refreshSecret;
        _accessExpiry = accessExpiry;
        _refreshExpiry = refreshExpiry;
        _issuer = issuer;
        _audience = audience;
    }

    public string GenerateAccessToken(Dictionary<string, string> claims)
    {
        return GenerateToken(claims, _secret, _accessExpiry);
    }

    public string GenerateRefreshToken(Dictionary<string, string> claims)
    {
        return GenerateToken(claims, _refreshSecret, _refreshExpiry);
    }

    /// <summary>
    /// Generate a long-lived emergency offline token (30 days) signed with the access secret.
    /// Intended for storage in IndexedDB — PIN protection is the responsibility of the client.
    /// </summary>
    public string GenerateEmergencyToken(Dictionary<string, string> claims)
    {
        return GenerateToken(claims, _secret, "720h");
    }

    /// <summary>
    /// Generate a long-lived machine token (365 days) for a Gaming PC agent.
    /// Used for the Client Agent to authenticate with the backend via SignalR and REST.
    /// </summary>
    public string GenerateAgentToken(string pcId, string branchId, string pcNumber)
    {
        var claims = new Dictionary<string, string>
        {
            { ClaimTypes.NameIdentifier, pcId },
            { ClaimTypes.Role, "Agent" },
            { "branchId", branchId },
            { "pcNumber", pcNumber },
            { "token_type", "machine_agent" }
        };
        return GenerateToken(claims, _secret, "8760h"); // 365 days
    }

    public ClaimsPrincipal? ValidateRefreshToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_refreshSecret);

        try
        {
            return tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
            }, out _);
        }
        catch
        {
            return null;
        }
    }

    private string GenerateToken(Dictionary<string, string> claimsDict, string secret, string expiry)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        foreach (var kvp in claimsDict)
        {
            if (!string.IsNullOrEmpty(kvp.Value))
                claims.Add(new Claim(kvp.Key, kvp.Value));
        }

        var expiresIn = ParseExpiry(expiry);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(expiresIn),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static TimeSpan ParseExpiry(string expiry)
    {
        // Supports formats: "15m", "1h", "7d", "24h"
        if (string.IsNullOrEmpty(expiry)) return TimeSpan.FromHours(1);

        var value = int.Parse(expiry[..^1]);
        return expiry[^1] switch
        {
            'm' => TimeSpan.FromMinutes(value),
            'h' => TimeSpan.FromHours(value),
            'd' => TimeSpan.FromDays(value),
            _ => TimeSpan.FromHours(1),
        };
    }
}
