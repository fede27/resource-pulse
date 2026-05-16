using FluentValidation;

namespace ResourcePulse.Services.Ping;

public sealed class CreatePingDtoValidator : AbstractValidator<CreatePingDto>
{
    public CreatePingDtoValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(500);
    }
}
