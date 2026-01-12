using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.DTOs;
using PassManAPI.Models;
using PassManAPI.Managers;
using PassManAPI.Services;

namespace PassManAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager _userManager;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ILookupNormalizer _normalizer;
    private readonly IAuditLogger _auditLogger;

    public AuthController(
        ApplicationDbContext db,
        UserManager userManager,
        IPasswordHasher<User> passwordHasher,
        ILookupNormalizer normalizer,
        IAuditLogger auditLogger
    )
    {
        _db = db;
        _userManager = userManager;
        _passwordHasher = passwordHasher;
        _normalizer = normalizer;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// Registers a new user. Returns a placeholder token until JWT is added.
    /// </summary>
    [HttpPost("register")]
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

        var token = $"dev-token-{result.Data.Id}";
        await _auditLogger.LogAsync(result.Data.Id, AuditAction.UserRegistered, "User", result.Data.Id, "User registered");
        var response = new AuthResponse(token, ToProfile(result.Data));
        return CreatedAtAction(nameof(GetCurrentUser), new { }, response);
    }

    /// <summary>
    /// Authenticates a user and returns a placeholder token.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var normalizedEmail = _normalizer.NormalizeEmail(request.Email.Trim()) ?? request.Email.Trim().ToUpperInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return Unauthorized("Invalid credentials.");
        }

        var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verify == PasswordVerificationResult.Failed)
        {
            await _auditLogger.LogAsync(user.Id, AuditAction.FailedLoginAttempt, "User", user.Id, "Invalid password");
            return Unauthorized("Invalid credentials.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = $"dev-token-{user.Id}";
        await _auditLogger.LogAsync(user.Id, AuditAction.UserLoggedIn, "User", user.Id, "User logged in");
        return Ok(new AuthResponse(token, ToProfile(user)));
    }

    /// <summary>
    /// Returns the current user's profile using a dev-only X-UserId header.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser([FromHeader(Name = "X-UserId")] int? userId)
    {
        if (userId is null)
        {
            return Unauthorized("Missing X-UserId header (dev placeholder auth).");
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
        [FromHeader(Name = "X-UserId")] int? userId,
        [FromBody] UpdateProfileRequest request
    )
    {
        if (userId is null)
        {
            return Unauthorized("Missing X-UserId header (dev placeholder auth).");
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
    public async Task<IActionResult> DeleteAccount([FromHeader(Name = "X-UserId")] int? userId)
    {
        if (userId is null)
        {
            return Unauthorized("Missing X-UserId header (dev placeholder auth).");
        }

        var result = await _userManager.DeleteUserAsync(userId.Value);
        if (!result.Success)
        {
            return NotFound(result.Error ?? "User not found.");
        }

        await _auditLogger.LogAsync(userId.Value, AuditAction.UserDeleted, "User", userId.Value, "Account deleted");
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
}
