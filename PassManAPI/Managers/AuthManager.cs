using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.Models;
using PassManAPI.Services;

namespace PassManAPI.Managers;

/// <summary>
/// Implementation of authentication business logic.
/// </summary>
public class AuthManager : IAuthManager
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ILookupNormalizer _normalizer;
    private readonly ITokenService _tokenService;
    private readonly ITwoFactorService _twoFactorService;
    private readonly ILogger<AuthManager> _logger;

    // In-memory store for refresh tokens (in production, use database table)
    private static readonly Dictionary<string, RefreshTokenRecord> _refreshTokens = new();

    // In-memory store for pending 2FA logins (email -> partial auth)
    private static readonly Dictionary<string, Pending2FALogin> _pending2FALogins = new();

    private record RefreshTokenRecord(
        string Token,
        int UserId,
        DateTime CreatedAt,
        DateTime ExpiresAt,
        bool IsRevoked
    );

    private record Pending2FALogin(
        int UserId,
        string Email,
        string? UserName,
        DateTime CreatedAt
    );

    public AuthManager(
        ApplicationDbContext db,
        IPasswordHasher<User> passwordHasher,
        ILookupNormalizer normalizer,
        ITokenService tokenService,
        ITwoFactorService twoFactorService,
        ILogger<AuthManager> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _normalizer = normalizer;
        _tokenService = tokenService;
        _twoFactorService = twoFactorService;
        _logger = logger;
    }

    public async Task<AuthResult<LoginResult>> RegisterAsync(
        string email, string password, string? userName = null, string? phoneNumber = null)
    {
        email = email.Trim();

        // Check if email already exists
        var normalizedEmail = _normalizer.NormalizeEmail(email) ?? email.ToUpperInvariant();
        var emailExists = await _db.Users.AsNoTracking()
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail);

        if (emailExists)
        {
            return AuthResult<LoginResult>.Fail("Email is already registered.");
        }

        // Create user
        var user = new User
        {
            Email = email,
            NormalizedEmail = normalizedEmail,
            UserName = userName ?? email,
            NormalizedUserName = _normalizer.NormalizeName(userName ?? email) ?? (userName ?? email).ToUpperInvariant(),
            PhoneNumber = phoneNumber,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true, // For now, skip email confirmation
            SecurityStamp = Guid.NewGuid().ToString()
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("User registered: {Email}", email);

        // Generate tokens and return
        return await GenerateLoginResultAsync(user);
    }

    public async Task<AuthResult<LoginResult>> LoginAsync(string email, string password)
    {
        var user = await ValidateCredentialsAsync(email, password);

        if (user == null)
        {
            _logger.LogWarning("Failed login attempt for: {Email}", email);
            return AuthResult<LoginResult>.Fail("Invalid email or password.");
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Check if 2FA is enabled
        if (user.TwoFactorEnabled && !string.IsNullOrEmpty(user.TotpSecret))
        {
            // Store pending 2FA login
            _pending2FALogins[email.ToLowerInvariant()] = new Pending2FALogin(
                user.Id,
                user.Email!,
                user.UserName,
                DateTime.UtcNow
            );

            _logger.LogInformation("2FA required for login: {Email}", email);
            return AuthResult<LoginResult>.TwoFactorRequired();
        }

        _logger.LogInformation("User logged in: {Email}", email);
        return await GenerateLoginResultAsync(user);
    }

    public async Task<AuthResult<LoginResult>> LoginWith2FAAsync(string email, string code)
    {
        var key = email.ToLowerInvariant();

        if (!_pending2FALogins.TryGetValue(key, out var pending))
        {
            return AuthResult<LoginResult>.Fail("No pending 2FA login found. Please start login again.");
        }

        // Check if pending login has expired (5 minutes)
        if (DateTime.UtcNow - pending.CreatedAt > TimeSpan.FromMinutes(5))
        {
            _pending2FALogins.Remove(key);
            return AuthResult<LoginResult>.Fail("2FA verification expired. Please start login again.");
        }

        var user = await _db.Users.FindAsync(pending.UserId);
        if (user == null || string.IsNullOrEmpty(user.TotpSecret))
        {
            _pending2FALogins.Remove(key);
            return AuthResult<LoginResult>.Fail("User not found.");
        }

        // Validate 2FA code
        if (!_twoFactorService.ValidateCode(user.TotpSecret, code))
        {
            _logger.LogWarning("Invalid 2FA code for: {Email}", email);
            return AuthResult<LoginResult>.Fail("Invalid 2FA code.");
        }

        _pending2FALogins.Remove(key);
        _logger.LogInformation("User logged in with 2FA: {Email}", email);
        return await GenerateLoginResultAsync(user);
    }

    public async Task<AuthResult<AuthTokens>> RefreshTokenAsync(string refreshToken)
    {
        if (!_refreshTokens.TryGetValue(refreshToken, out var record))
        {
            return AuthResult<AuthTokens>.Fail("Invalid refresh token.");
        }

        if (record.IsRevoked)
        {
            return AuthResult<AuthTokens>.Fail("Refresh token has been revoked.");
        }

        if (DateTime.UtcNow > record.ExpiresAt)
        {
            _refreshTokens.Remove(refreshToken);
            return AuthResult<AuthTokens>.Fail("Refresh token has expired.");
        }

        var user = await _db.Users.FindAsync(record.UserId);
        if (user == null)
        {
            return AuthResult<AuthTokens>.Fail("User not found.");
        }

        // Revoke old token and generate new ones
        _refreshTokens.Remove(refreshToken);

        var tokens = GenerateTokens(user);

        _logger.LogInformation("Token refreshed for user: {UserId}", user.Id);
        return AuthResult<AuthTokens>.Ok(tokens);
    }

    public async Task<AuthResult<bool>> LogoutAsync(int userId, string refreshToken)
    {
        if (_refreshTokens.TryGetValue(refreshToken, out var record))
        {
            if (record.UserId != userId)
            {
                return AuthResult<bool>.Fail("Token does not belong to this user.");
            }

            _refreshTokens.Remove(refreshToken);
        }

        _logger.LogInformation("User logged out: {UserId}", userId);
        return AuthResult<bool>.Ok(true);
    }

    public async Task<User?> ValidateCredentialsAsync(string email, string password)
    {
        var normalizedEmail = _normalizer.NormalizeEmail(email) ?? email.ToUpperInvariant();

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

        if (user == null || string.IsNullOrEmpty(user.PasswordHash))
        {
            return null;
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        // If password needs rehashing, do it now
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
            await _db.SaveChangesAsync();
        }

        return user;
    }

    public async Task<AuthResult<TwoFactorSetupResult>> Enable2FAAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            return AuthResult<TwoFactorSetupResult>.Fail("User not found.");
        }

        if (user.TwoFactorEnabled)
        {
            return AuthResult<TwoFactorSetupResult>.Fail("2FA is already enabled.");
        }

        // Generate secret (don't save until verified)
        var secret = _twoFactorService.GenerateSecretKey();
        var qrCodeUri = _twoFactorService.GenerateQrCodeUri(secret, user.Email!, "PassMan");
        var backupCodes = _twoFactorService.GenerateBackupCodes();

        // Store secret temporarily (not enabled yet)
        user.TotpSecret = secret;
        await _db.SaveChangesAsync();

        _logger.LogInformation("2FA setup initiated for user: {UserId}", userId);

        return AuthResult<TwoFactorSetupResult>.Ok(new TwoFactorSetupResult(
            secret,
            qrCodeUri,
            backupCodes
        ));
    }

    public async Task<AuthResult<bool>> Verify2FASetupAsync(int userId, string code)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            return AuthResult<bool>.Fail("User not found.");
        }

        if (string.IsNullOrEmpty(user.TotpSecret))
        {
            return AuthResult<bool>.Fail("2FA setup has not been initiated.");
        }

        if (user.TwoFactorEnabled)
        {
            return AuthResult<bool>.Fail("2FA is already enabled.");
        }

        if (!_twoFactorService.ValidateCode(user.TotpSecret, code))
        {
            return AuthResult<bool>.Fail("Invalid 2FA code. Please try again.");
        }

        // Enable 2FA
        user.TwoFactorEnabled = true;
        await _db.SaveChangesAsync();

        _logger.LogInformation("2FA enabled for user: {UserId}", userId);

        return AuthResult<bool>.Ok(true);
    }

    public async Task<AuthResult<bool>> Disable2FAAsync(int userId, string code)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            return AuthResult<bool>.Fail("User not found.");
        }

        if (!user.TwoFactorEnabled || string.IsNullOrEmpty(user.TotpSecret))
        {
            return AuthResult<bool>.Fail("2FA is not enabled.");
        }

        if (!_twoFactorService.ValidateCode(user.TotpSecret, code))
        {
            return AuthResult<bool>.Fail("Invalid 2FA code.");
        }

        // Disable 2FA
        user.TwoFactorEnabled = false;
        user.TotpSecret = null;
        await _db.SaveChangesAsync();

        _logger.LogInformation("2FA disabled for user: {UserId}", userId);

        return AuthResult<bool>.Ok(true);
    }

    public async Task<bool> Validate2FACodeAsync(int userId, string code)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null || string.IsNullOrEmpty(user.TotpSecret))
        {
            return false;
        }

        return _twoFactorService.ValidateCode(user.TotpSecret, code);
    }

    private async Task<AuthResult<LoginResult>> GenerateLoginResultAsync(User user)
    {
        var tokens = GenerateTokens(user);

        return AuthResult<LoginResult>.Ok(new LoginResult(
            user.Id,
            user.Email!,
            user.UserName,
            tokens,
            user.TwoFactorEnabled
        ));
    }

    private AuthTokens GenerateTokens(User user)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        var accessExpires = DateTime.UtcNow.AddMinutes(_tokenService.AccessTokenExpirationMinutes);
        var refreshExpires = DateTime.UtcNow.AddDays(_tokenService.RefreshTokenExpirationDays);

        // Store refresh token
        _refreshTokens[refreshToken] = new RefreshTokenRecord(
            refreshToken,
            user.Id,
            DateTime.UtcNow,
            refreshExpires,
            false
        );

        return new AuthTokens(
            accessToken,
            refreshToken,
            accessExpires,
            refreshExpires
        );
    }
}
