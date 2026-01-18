using FluentValidation;
using static PassManAPI.Controllers.VaultsController;

namespace PassManAPI.Validators;

/// <summary>
/// FluentValidation validator for vault creation requests.
/// </summary>
public class CreateVaultRequestValidator : AbstractValidator<CreateVaultRequest>
{
    public CreateVaultRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Vault name is required.")
            .MaximumLength(100).WithMessage("Vault name cannot exceed 100 characters.")
            .Matches(@"^[a-zA-Z0-9\s\-_]+$").WithMessage("Vault name contains invalid characters. Only letters, numbers, spaces, hyphens, and underscores are allowed.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("Invalid user ID.");
    }
}

/// <summary>
/// FluentValidation validator for vault update requests.
/// </summary>
public class UpdateVaultRequestValidator : AbstractValidator<UpdateVaultRequest>
{
    public UpdateVaultRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Vault name is required.")
            .MaximumLength(100).WithMessage("Vault name cannot exceed 100 characters.")
            .Matches(@"^[a-zA-Z0-9\s\-_]+$").WithMessage("Vault name contains invalid characters. Only letters, numbers, spaces, hyphens, and underscores are allowed.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
