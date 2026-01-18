using PassManAPI.Models;

namespace PassManAPI.Managers;

/// <summary>
/// Result object for authentication operations.
/// </summary>
public class AuthResult<T>
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public T? Data { get; init; }
    public bool Requires2FA { get; init; }

    public static AuthResult<T> Ok(T data) =>
        new() { Success = true, Data = data };

    public static AuthResult<T> Fail(string error) =>
        new() { Success = false, Error = error };

    public static AuthResult<T> TwoFactorRequired() =>
        new() { Success = false, Requires2FA = true };
}

/// <summary>
/// DTO for authentication tokens.
/// </summary>
public record AuthTokens(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpires,
    DateTime RefreshTokenExpires
);

/// <summary>
/// DTO for login result including user info.
/// </summary>
public record LoginResult(
    int UserId,
    string Email,
    string? UserName,
    AuthTokens Tokens,
    bool TwoFactorEnabled
);

/// <summary>
/// DTO for 2FA setup result.
/// </summary>
public record TwoFactorSetupResult(
    string Secret,
    string QrCodeUri,
    IList<string> BackupCodes
);

/// <summary>
/// Interface for authentication business logic.
/// </summary>
public interface IAuthManager
{
    /// <summary>
    /// Registers a new user with email and password.
    /// </summary>
    /// <param name="email">User's email address.</param>
    /// <param name="password">User's password.</param>
    /// <param name="userName">Optional username.</param>
    /// <param name="phoneNumber">Optional phone number.</param>
    /// <returns>Result containing login result on success or error.</returns>
    Task<AuthResult<LoginResult>> RegisterAsync(string email, string password, string? userName = null, string? phoneNumber = null);

    /// <summary>
    /// Authenticates a user with email and password.
    /// </summary>
    /// <param name="email">User's email address.</param>
    /// <param name="password">User's password.</param>
    /// <returns>Result containing login result, 2FA requirement, or error.</returns>
    Task<AuthResult<LoginResult>> LoginAsync(string email, string password);

    /// <summary>
    /// Completes login with 2FA verification.
    /// </summary>
    /// <param name="email">User's email address.</param>
    /// <param name="code">The 2FA code.</param>
    /// <returns>Result containing login result or error.</returns>
    Task<AuthResult<LoginResult>> LoginWith2FAAsync(string email, string code);

    /// <summary>
    /// Refreshes an expired access token using a valid refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token.</param>
    /// <returns>Result containing new tokens or error.</returns>
    Task<AuthResult<AuthTokens>> RefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Logs out a user by invalidating their refresh token.
    /// </summary>
    /// <param name="userId">User's ID.</param>
    /// <param name="refreshToken">The refresh token to invalidate.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<AuthResult<bool>> LogoutAsync(int userId, string refreshToken);

    /// <summary>
    /// Validates user credentials without creating a session.
    /// </summary>
    /// <param name="email">User's email address.</param>
    /// <param name="password">User's password.</param>
    /// <returns>The user if credentials are valid, null otherwise.</returns>
    Task<User?> ValidateCredentialsAsync(string email, string password);

    /// <summary>
    /// Enables 2FA for a user by generating and storing a TOTP secret.
    /// </summary>
    /// <param name="userId">User's ID.</param>
    /// <returns>Result containing setup info (secret, QR code URI, backup codes) or error.</returns>
    Task<AuthResult<TwoFactorSetupResult>> Enable2FAAsync(int userId);

    /// <summary>
    /// Verifies and finalizes 2FA setup.
    /// </summary>
    /// <param name="userId">User's ID.</param>
    /// <param name="code">The verification code to confirm 2FA setup.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<AuthResult<bool>> Verify2FASetupAsync(int userId, string code);

    /// <summary>
    /// Disables 2FA for a user (requires valid 2FA code for verification).
    /// </summary>
    /// <param name="userId">User's ID.</param>
    /// <param name="code">The 2FA code for verification.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<AuthResult<bool>> Disable2FAAsync(int userId, string code);

    /// <summary>
    /// Validates a 2FA code for a user.
    /// </summary>
    /// <param name="userId">User's ID.</param>
    /// <param name="code">The 2FA code to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    Task<bool> Validate2FACodeAsync(int userId, string code);
}
