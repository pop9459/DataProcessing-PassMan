using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PassManAPI.Models;

namespace PassManAPI.Services;

public interface IJwtTokenService
{
    // Issues a short-lived access token for the given user, including the user's
    // role and permission claims so authorization policies can be satisfied.
    Task<string> CreateAccessTokenAsync(User user);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole<int>> _roleManager;

    public JwtTokenService(
        IOptions<JwtOptions> options,
        UserManager<User> userManager,
        RoleManager<IdentityRole<int>> roleManager)
    {
        _options = options.Value;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<string> CreateAccessTokenAsync(User user)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            throw new InvalidOperationException("JWT signing key is not configured.");
        }

        // Symmetric key signing for dev/prod (keep key secret).
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Base claims: user id, email, name, and token id.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Embed the user's roles and their associated permission claims so that the
        // authorization policies (which RequireClaim "permission") are satisfied.
        // Without this, every permission-protected endpoint returns 403 under JWT auth.
        // Mirrors DevHeaderAuthenticationHandler so JWT and dev-header auth behave identically.
        var roles = await _userManager.GetRolesAsync(user);
        foreach (var roleName in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, roleName));

            var role = await _roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                continue;
            }

            var roleClaims = await _roleManager.GetClaimsAsync(role);
            claims.AddRange(
                roleClaims.Where(c => c.Type == PermissionConstants.ClaimType)
            );
        }

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
