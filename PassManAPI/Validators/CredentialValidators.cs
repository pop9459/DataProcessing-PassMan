using FluentValidation;
using static PassManAPI.Controllers.CredentialsController;

namespace PassManAPI.Validators;

/// <summary>
/// FluentValidation validator for credential creation requests.
/// </summary>
public class CreateCredentialRequestValidator : AbstractValidator<CreateCredentialRequest>
{
    public CreateCredentialRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Credential title is required.")
            .MaximumLength(255).WithMessage("Title cannot exceed 255 characters.");

        RuleFor(x => x.Username)
            .MaximumLength(255).WithMessage("Username cannot exceed 255 characters.")
            .When(x => !string.IsNullOrEmpty(x.Username));

        RuleFor(x => x.EncryptedPassword)
            .NotEmpty().WithMessage("Encrypted password is required.");

        RuleFor(x => x.Url)
            .MaximumLength(500).WithMessage("URL cannot exceed 500 characters.")
            .Must(BeAValidUrl).WithMessage("URL format is invalid.")
            .When(x => !string.IsNullOrEmpty(x.Url));

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("Invalid category ID.")
            .When(x => x.CategoryId.HasValue);
    }

    private static bool BeAValidUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return true;
        return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
               (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}

/// <summary>
/// FluentValidation validator for credential update requests.
/// </summary>
public class UpdateCredentialRequestValidator : AbstractValidator<UpdateCredentialRequest>
{
    public UpdateCredentialRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Credential title is required.")
            .MaximumLength(255).WithMessage("Title cannot exceed 255 characters.");

        RuleFor(x => x.Username)
            .MaximumLength(255).WithMessage("Username cannot exceed 255 characters.")
            .When(x => !string.IsNullOrEmpty(x.Username));

        RuleFor(x => x.Url)
            .MaximumLength(500).WithMessage("URL cannot exceed 500 characters.")
            .Must(BeAValidUrl).WithMessage("URL format is invalid.")
            .When(x => !string.IsNullOrEmpty(x.Url));

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("Invalid category ID.")
            .When(x => x.CategoryId.HasValue);
    }

    private static bool BeAValidUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return true;
        return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
               (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}
