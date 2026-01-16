using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassManAPI.Models;

namespace PassManAPI.Controllers;

[ApiController]
[Route("api/vaults/{vaultId}/share")]
public class VaultSharesController : ControllerBase
{
    /// <summary>
    /// Shares a vault with another user.
    /// </summary>
    /// <remarks>
    /// Grants access to a specific vault to another user via their email.
    /// </remarks>
    /// <param name="vaultId">The unique identifier of the vault.</param>
    /// <param name="request">The sharing details (user email, permission level).</param>
    /// <response code="200">If the share is successful.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have permission to share the vault.</response>
    /// <response code="404">If the vault or user is not found.</response>
    [HttpPost]
    [Authorize(Policy = PermissionConstants.VaultShare)]
    public IActionResult ShareVault(int vaultId, [FromBody] object request)
    {
        // TODO: Implement vault sharing logic
        return Ok("Not implemented");
    }

    /// <summary>
    /// Revokes access to a vault for a user.
    /// </summary>
    /// <remarks>
    /// Removes a user's access to a specific vault.
    /// </remarks>
    /// <param name="vaultId">The unique identifier of the vault.</param>
    /// <param name="userId">The unique identifier of the user to remove.</param>
    /// <response code="204">If the access is revoked successfully.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have permission to revoke access.</response>
    /// <response code="404">If the vault or user share is not found.</response>
    [HttpDelete("{userId}")]
    [Authorize(Policy = PermissionConstants.VaultShare)]
    public IActionResult RevokeShare(int vaultId, int userId)
    {
        // TODO: Implement share revocation logic
        return Ok("Not implemented");
    }
}
