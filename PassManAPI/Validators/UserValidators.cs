using FluentValidation;
using PassManAPI.DTOs;

namespace PassManAPI.Validators;

/// <summary>
/// FluentValidation validator for user registration requests.
/// </summary>
public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email cannot exceed 256 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(100).WithMessage("Password cannot exceed 100 characters.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"\d").WithMessage("Password must contain at least one digit.")
            .Matches(@"[^A-Za-z0-9]").WithMessage("Password must contain at least one non-alphanumeric character.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Password confirmation is required.")
            .Equal(x => x.Password).WithMessage("Passwords must match.");

        RuleFor(x => x.UserName)
            .MaximumLength(256).WithMessage("Username cannot exceed 256 characters.")
            .Matches(@"^[a-zA-Z0-9\-_\.]*$").WithMessage("Username can only contain letters, numbers, hyphens, underscores, and periods.")
            .When(x => !string.IsNullOrEmpty(x.UserName));

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^[\d\+\-\(\)\s]*$").WithMessage("Phone number format is invalid.")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }
}

/// <summary>
/// FluentValidation validator for login requests.
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email cannot exceed 256 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}

/// <summary>
/// FluentValidation validator for profile update requests.
/// </summary>
public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email cannot exceed 256 characters.")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.UserName)
            .MaximumLength(256).WithMessage("Username cannot exceed 256 characters.")
            .Matches(@"^[a-zA-Z0-9\-_\.]*$").WithMessage("Username can only contain letters, numbers, hyphens, underscores, and periods.")
            .When(x => !string.IsNullOrEmpty(x.UserName));

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^[\d\+\-\(\)\s]*$").WithMessage("Phone number format is invalid.")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }
}
