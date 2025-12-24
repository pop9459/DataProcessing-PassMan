using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace PassManAPI.Validation;

/// <summary>
/// Enforces the Identity password rules configured in Program.cs:
/// - Minimum length 8
/// - At least one uppercase, one lowercase, one digit, and one non-alphanumeric character.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class PasswordComplexityAttribute : ValidationAttribute
{
    private const string DefaultMessage =
        "Password must be at least 8 characters and include uppercase, lowercase, digit, and non-alphanumeric characters.";

    private static readonly Regex Pattern = new(
        "^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[^A-Za-z0-9]).{8,}$",
        RegexOptions.Compiled
    );

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success; // let [Required] handle null if present
        }

        if (value is not string password)
        {
            return new ValidationResult(ErrorMessage ?? DefaultMessage);
        }

        if (password.Length == 0)
        {
            return ValidationResult.Success; // empty handled by [Required] when needed
        }

        return Pattern.IsMatch(password)
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage ?? DefaultMessage);
    }
}

