using BCryptNet = BCrypt.Net.BCrypt;
using Microsoft.AspNetCore.Identity;
using PassManAPI.Models;

namespace PassManAPI.Helpers;

/// <summary>
/// BCrypt-backed password hasher used for user creation and verification.
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher<User>
{
    private const int WorkFactor = 12;

    public string HashPassword(User user, string password)
    {
        return BCryptNet.HashPassword(password, workFactor: WorkFactor);
    }

    public PasswordVerificationResult VerifyHashedPassword(
        User user,
        string hashedPassword,
        string providedPassword
    )
    {
        var valid = BCryptNet.Verify(providedPassword, hashedPassword);
        return valid ? PasswordVerificationResult.Success : PasswordVerificationResult.Failed;
    }
}

