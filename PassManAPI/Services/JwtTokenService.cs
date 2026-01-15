using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PassManAPI.Models;

namespace PassManAPI.Services;

public interface IJwtTokenService
{
    // Issues a short-lived access token for the given user.
    string CreateAccessToken(User user);
    // Overload used when we only have a response DTO, not the full entity.
    string CreateAccessToken(int userId, string email, string? userName);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string CreateAccessToken(User user)
    {
        return CreateAccessToken(user.Id, user.Email ?? string.Empty, user.UserName);
    }

    public string CreateAccessToken(int userId, string email, string? userName)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            throw new InvalidOperationException("JWT signing key is not configured.");
        }

        // Symmetric key signing for dev/prod (keep key secret).
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Minimal claims: user id, email, name, and token id.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email ?? string.Empty),
            new(ClaimTypes.Name, userName ?? email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenLifetimeMinutes),
            signingCredentials: creds
        );

        // Serialize JWT for the client to send as Bearer token.
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PassManAPI.Models;

namespace PassManAPI.Services;

public interface IJwtTokenService
{
    // Issues a short-lived access token for the given user.
    string CreateAccessToken(User user);
    // Overload used when we only have a response DTO, not the full entity.
    string CreateAccessToken(int userId, string email, string? userName);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string CreateAccessToken(User user)
    {
        return CreateAccessToken(user.Id, user.Email ?? string.Empty, user.UserName);
    }

    public string CreateAccessToken(int userId, string email, string? userName)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            throw new InvalidOperationException("JWT signing key is not configured.");
        }

        // Symmetric key signing for dev/prod (keep key secret).
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Minimal claims: user id, email, name, and token id.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email ?? string.Empty),
            new(ClaimTypes.Name, userName ?? email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenLifetimeMinutes),
            signingCredentials: creds
        );

        // Serialize JWT for the client to send as Bearer token.
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
