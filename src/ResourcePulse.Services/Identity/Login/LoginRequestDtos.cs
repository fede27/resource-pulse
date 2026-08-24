using FluentValidation;

namespace ResourcePulse.Services.Identity.Login;

/// <summary>Credentials submitted by our own login page (ADR-0031).</summary>
public sealed record LoginRequestDto
{
    /// <summary>The Zitadel authorization request this sign-in belongs to.</summary>
    public required string AuthRequestId { get; init; }

    /// <summary>Email or username. Zitadel resolves it; we never parse it.</summary>
    public required string LoginName { get; init; }

    public required string Password { get; init; }
}

/// <summary>The second step, when the password was correct but must be replaced.</summary>
public sealed record ChangePasswordRequestDto
{
    public required string AuthRequestId { get; init; }

    public required string NewPassword { get; init; }
}

public sealed class LoginRequestDtoValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestDtoValidator()
    {
        RuleFor(x => x.AuthRequestId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LoginName).NotEmpty().MaximumLength(200);
        // Only the bounds Zitadel itself imposes. Complexity is the instance's
        // password policy to judge, and duplicating it here would drift.
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}

public sealed class ChangePasswordRequestDtoValidator : AbstractValidator<ChangePasswordRequestDto>
{
    public ChangePasswordRequestDtoValidator()
    {
        RuleFor(x => x.AuthRequestId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NewPassword).NotEmpty().MaximumLength(200);
    }
}
