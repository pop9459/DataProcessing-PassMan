using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PassManAPI.Models;

namespace PassManAPI.Helpers;

/// <summary>
/// Development/test authentication handler that builds a ClaimsPrincipal from the
/// X-UserId header. Roles and permission claims are loaded from Identity so we can
/// exercise authorization policies without a full JWT flow.
/// </summary>
public class DevHeaderAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Scheme = "DevHeader";
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole<int>> _roleManager;

    public DevHeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        UserManager<User> userManager,
        RoleManager<IdentityRole<int>> roleManager)
        : base(options, logger, encoder, clock)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-UserId", out var userIdHeader))
        {
            return AuthenticateResult.NoResult();
        }

        if (!int.TryParse(userIdHeader.ToString(), out var userId))
        {
            return AuthenticateResult.Fail("Invalid X-UserId header");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AuthenticateResult.Fail("User not found");
        }

        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? $"user-{user.Id}")
        };

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

        var identity = new ClaimsIdentity(claims, Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme);
        return AuthenticateResult.Success(ticket);
    }
}

