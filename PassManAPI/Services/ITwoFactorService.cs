namespace PassManAPI.Services;

/// <summary>
/// Service interface for Time-based One-Time Password (TOTP) two-factor authentication.
/// </summary>
public interface ITwoFactorService
{
    /// <summary>
    /// Generates a new TOTP secret key for a user.
    /// </summary>
    /// <returns>A Base32-encoded secret key.</returns>
    string GenerateSecretKey();

    /// <summary>
    /// Generates a TOTP code for the given secret.
    /// </summary>
    /// <param name="secretKey">The Base32-encoded secret key.</param>
    /// <returns>The current 6-digit TOTP code.</returns>
    string GenerateCode(string secretKey);

    /// <summary>
    /// Validates a TOTP code against the secret.
    /// </summary>
    /// <param name="secretKey">The Base32-encoded secret key.</param>
    /// <param name="code">The 6-digit code to validate.</param>
    /// <param name="toleranceSteps">Number of time steps to allow for clock drift (default: 1).</param>
    /// <returns>True if the code is valid, false otherwise.</returns>
    bool ValidateCode(string secretKey, string code, int toleranceSteps = 1);

    /// <summary>
    /// Generates a provisioning URI for authenticator apps (Google Authenticator, Authy, etc.).
    /// </summary>
    /// <param name="secretKey">The Base32-encoded secret key.</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="issuer">The application name (e.g., "PassMan").</param>
    /// <returns>An otpauth:// URI for QR code generation.</returns>
    string GenerateQrCodeUri(string secretKey, string email, string issuer = "PassMan");

    /// <summary>
    /// Generates backup codes for account recovery.
    /// </summary>
    /// <param name="count">Number of backup codes to generate (default: 10).</param>
    /// <returns>A list of single-use backup codes.</returns>
    IList<string> GenerateBackupCodes(int count = 10);
}
