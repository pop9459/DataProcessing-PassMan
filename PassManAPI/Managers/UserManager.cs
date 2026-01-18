using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.Models;

namespace PassManAPI.Managers;

public record CreateUserRequest(
    string Email,
    string Password,
    string? UserName = null,
    string? PhoneNumber = null,
    string? EncryptedVaultKey = null
);

public record UpdateUserRequest(
    string? Email = null,
    string? UserName = null,
    string? PhoneNumber = null,
    string? EncryptedVaultKey = null
);

public record UserResponse(
    int Id,
    string Email,
    string? UserName,
    string? PhoneNumber,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastLoginAt,
    string? EncryptedVaultKey,
    Guid? SubscriptionTierId
);

public class UserOperationResult<T>
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public T? Data { get; init; }

    public static UserOperationResult<T> Ok(T data) =>
        new() { Success = true, Data = data };

    public static UserOperationResult<T> Fail(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// Light-weight user manager that handles CRUD without authentication flows.
/// Passwords are hashed with BCrypt via the configured IPasswordHasher.
/// </summary>
public class UserManager
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ILookupNormalizer _normalizer;

    public UserManager(
        ApplicationDbContext db,
        IPasswordHasher<User> passwordHasher,
        ILookupNormalizer normalizer
    )
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _normalizer = normalizer;
    }

    public async Task<UserOperationResult<UserResponse>> CreateUserAsync(CreateUserRequest request)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            return UserOperationResult<UserResponse>.Fail("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return UserOperationResult<UserResponse>.Fail("Password is required.");
        }

        var normalizedEmail = _normalizer.NormalizeEmail(email) ?? email.ToUpperInvariant();
        var emailInUse = await _db.Users.AsNoTracking().AnyAsync(u => u.NormalizedEmail == normalizedEmail);
        if (emailInUse)
        {
            return UserOperationResult<UserResponse>.Fail("Email is already in use.");
        }

        var userName = string.IsNullOrWhiteSpace(request.UserName) ? email : request.UserName.Trim();
        var normalizedUserName = _normalizer.NormalizeName(userName) ?? userName.ToUpperInvariant();

        var usernameInUse = await _db.Users.AsNoTracking().AnyAsync(u => u.NormalizedUserName == normalizedUserName);
        if (usernameInUse)
        {
            return UserOperationResult<UserResponse>.Fail("Username is already in use.");
        }

        var user = new User
        {
            Email = email,
            NormalizedEmail = normalizedEmail,
            UserName = userName,
            NormalizedUserName = normalizedUserName,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            EncryptedVaultKey = request.EncryptedVaultKey,
            CreatedAt = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return UserOperationResult<UserResponse>.Ok(ToResponse(user));
    }

    public async Task<UserOperationResult<UserResponse>> GetUserByIdAsync(int id)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return UserOperationResult<UserResponse>.Fail("User not found.");
        }

        return UserOperationResult<UserResponse>.Ok(ToResponse(user));
    }

    public async Task<UserOperationResult<UserResponse>> UpdateUserAsync(int id, UpdateUserRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return UserOperationResult<UserResponse>.Fail("User not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var email = request.Email.Trim();
            var normalizedEmail = _normalizer.NormalizeEmail(email) ?? email.ToUpperInvariant();
            var emailInUse = await _db.Users.AsNoTracking().AnyAsync(u => u.Id != id && u.NormalizedEmail == normalizedEmail);
            if (emailInUse)
            {
                return UserOperationResult<UserResponse>.Fail("Email is already in use.");
            }

            user.Email = email;
            user.NormalizedEmail = normalizedEmail;
            user.ConcurrencyStamp = Guid.NewGuid().ToString();
        }

        if (!string.IsNullOrWhiteSpace(request.UserName))
        {
            var userName = request.UserName.Trim();
            user.UserName = userName;
            var normalizedUserName = _normalizer.NormalizeName(userName) ?? userName.ToUpperInvariant();

            var usernameInUse = await _db.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id != id && u.NormalizedUserName == normalizedUserName);
            if (usernameInUse)
            {
                return UserOperationResult<UserResponse>.Fail("Username is already in use.");
            }

            user.NormalizedUserName = normalizedUserName;
            user.ConcurrencyStamp = Guid.NewGuid().ToString();
        }

        if (request.PhoneNumber is not null)
        {
            user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
                ? null
                : request.PhoneNumber.Trim();
        }

        if (request.EncryptedVaultKey is not null)
        {
            user.EncryptedVaultKey = request.EncryptedVaultKey;
        }

        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return UserOperationResult<UserResponse>.Ok(ToResponse(user));
    }

    public async Task<UserOperationResult<bool>> DeleteUserAsync(int id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return UserOperationResult<bool>.Fail("User not found.");
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        return UserOperationResult<bool>.Ok(true);
    }

    private static UserResponse ToResponse(User user) =>
        new(
            user.Id,
            user.Email ?? string.Empty,
            user.UserName,
            user.PhoneNumber,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt,
            user.EncryptedVaultKey,
            user.SubscriptionTierId
        );
}

