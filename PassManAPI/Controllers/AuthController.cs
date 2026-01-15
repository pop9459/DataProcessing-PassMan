using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using PassManAPI.DTOs;
using PassManAPI.Models;
using PassManAPI.Managers;
using PassManAPI.Services;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace PassManAPI.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthController : ControllerBase
{
    private readonly PassManAPI.Managers.UserManager _userManager;
    private readonly Microsoft.AspNetCore.Identity.UserManager<User> _identityUserManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(
        PassManAPI.Managers.UserManager userManager,
        Microsoft.AspNetCore.Identity.UserManager<User> identityUserManager,
        SignInManager<User> signInManager,
        IJwtTokenService jwtTokenService
    )
    {
        _userManager = userManager;
        _identityUserManager = identityUserManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    /// Registers a new user and returns a JWT access token.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _userManager.CreateUserAsync(
            new CreateUserRequest(
                request.Email,
                request.Password,
                request.UserName,
                request.PhoneNumber,
                request.EncryptedVaultKey
            )
        );

        if (!result.Success || result.Data is null)
        {
            return BadRequest(result.Error ?? "Registration failed.");
        }

        var token = _jwtTokenService.CreateAccessToken(
            result.Data.Id,
            result.Data.Email,
            result.Data.UserName
        );
        var response = new AuthResponse(token, ToProfile(result.Data));
        return CreatedAtAction(nameof(GetCurrentUser), new { }, response);
    }

    /// <summary>
    /// Authenticates a user and returns a JWT access token.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var email = request.Email.Trim();
        var user = await _identityUserManager.FindByEmailAsync(email);
        if (user is null)
        {
            return Unauthorized("Invalid credentials.");
        }

        if (!await _identityUserManager.IsEmailConfirmedAsync(user))
        {
            return Unauthorized("Email not confirmed.");
        }

        var signInResult = await _signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true
        );

        if (signInResult.IsLockedOut)
        {
            return StatusCode(StatusCodes.Status423Locked, "Account is locked. Try again later.");
        }

        if (!signInResult.Succeeded)
        {
            return Unauthorized("Invalid credentials.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _identityUserManager.UpdateAsync(user);

        var token = _jwtTokenService.CreateAccessToken(user);
        return Ok(new AuthResponse(token, ToProfile(user)));
    }

    /// <summary>
    /// Returns the current user's profile using JWT claims.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        // Resolve user id from JWT claims.
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _userManager.GetUserByIdAsync(userId.Value);
        if (!result.Success || result.Data is null)
        {
            return NotFound("User not found.");
        }

        return Ok(ToProfile(result.Data));
    }

    /// <summary>
    /// Updates the current user's profile (email/username/phone/encrypted key).
    /// </summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request
    )
    {
        // Resolve user id from JWT claims.
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _userManager.UpdateUserAsync(
            userId.Value,
            new UpdateUserRequest(
                request.Email,
                request.UserName,
                request.PhoneNumber,
                request.EncryptedVaultKey
            )
        );

        if (!result.Success || result.Data is null)
        {
            return BadRequest(result.Error ?? "Update failed.");
        }

        return Ok(ToProfile(result.Data));
    }

    /// <summary>
    /// Deletes the current user's account.
    /// </summary>
    [HttpDelete("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteAccount()
    {
        // Resolve user id from JWT claims.
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _userManager.DeleteUserAsync(userId.Value);
        if (!result.Success)
        {
            return NotFound(result.Error ?? "User not found.");
        }

        return NoContent();
    }

    private static UserProfileResponse ToProfile(User user) =>
        new(
            user.Id,
            user.Email ?? string.Empty,
            user.UserName,
            user.PhoneNumber,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt,
            user.EncryptedVaultKey
        );

    private static UserProfileResponse ToProfile(Managers.UserResponse user) =>
        new(
            user.Id,
            user.Email,
            user.UserName,
            user.PhoneNumber,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt,
            user.EncryptedVaultKey
        );

    // Reads the authenticated user id from standard JWT claims.
    private int? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ??
                    User.FindFirst(JwtRegisteredClaimNames.Sub);

        if (claim is null)
        {
            return null;
        }

        return int.TryParse(claim.Value, out var userId) ? userId : null;
    }
}
