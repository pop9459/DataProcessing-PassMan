using PassManAPI.Models;

namespace PassManAPI.Services;

/// <summary>
/// Service interface for JWT token generation and validation.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a JWT access token for the specified user.
    /// </summary>
    /// <param name="user">The user to generate the token for.</param>
    /// <returns>The generated JWT token string.</returns>
    string GenerateAccessToken(User user);

    /// <summary>
    /// Generates a refresh token for token renewal.
    /// </summary>
    /// <returns>A secure random refresh token string.</returns>
    string GenerateRefreshToken();

    /// <summary>
    /// Validates a JWT token and extracts the user ID.
    /// </summary>
    /// <param name="token">The JWT token to validate.</param>
    /// <returns>The user ID if valid, null otherwise.</returns>
    int? ValidateToken(string token);

    /// <summary>
    /// Gets the token expiration time in minutes.
    /// </summary>
    int AccessTokenExpirationMinutes { get; }

    /// <summary>
    /// Gets the refresh token expiration time in days.
    /// </summary>
    int RefreshTokenExpirationDays { get; }
}
