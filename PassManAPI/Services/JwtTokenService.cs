using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PassManAPI.Models;

namespace PassManAPI.Services;

public interface IJwtTokenService
{
    // Issues a short-lived access token for the given user.
    Task<string> CreateAccessToken(User user);
    // Overload used when we only have a response DTO, not the full entity.
    Task<string> CreateAccessToken(int userId, string email, string? userName);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole<int>> _roleManager;

    public JwtTokenService(
        IOptions<JwtOptions> options,
        UserManager<User> userManager,
        RoleManager<IdentityRole<int>> roleManager
    )
    {
        _options = options.Value;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<string> CreateAccessToken(User user)
    {
        return await CreateAccessToken(user.Id, user.Email ?? string.Empty, user.UserName);
    }

    public async Task<string> CreateAccessToken(int userId, string email, string? userName)
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

        // Mirror policy claims expected by authorization handlers.
        claims.AddRange(await BuildRoleAndPermissionClaimsAsync(userId));

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

    private async Task<IEnumerable<Claim>> BuildRoleAndPermissionClaimsAsync(int userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Array.Empty<Claim>();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var claims = new List<Claim>();
        var seenPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var roleName in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, roleName));

            var role = await _roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                continue;
            }

            var roleClaims = await _roleManager.GetClaimsAsync(role);
            foreach (var roleClaim in roleClaims.Where(c => c.Type == PermissionConstants.ClaimType))
            {
                if (seenPermissions.Add(roleClaim.Value))
                {
                    claims.Add(new Claim(PermissionConstants.ClaimType, roleClaim.Value));
                }
            }
        }

        return claims;
    }
}
