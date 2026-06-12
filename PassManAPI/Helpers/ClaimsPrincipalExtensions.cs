using System.Security.Claims;
using PassManAPI.Models;

namespace PassManAPI.Helpers;

public static class ClaimsPrincipalExtensions
{
    public static bool TryGetCurrentUserId(this ClaimsPrincipal user, out int userId)
    {
        userId = 0;
        var claim = user.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out userId);
    }

    public static bool HasPermission(this ClaimsPrincipal user, string permission) =>
        user.Claims.Any(c => c.Type == PermissionConstants.ClaimType && c.Value == permission);
}
