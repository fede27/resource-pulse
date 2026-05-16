using FluentValidation;

namespace ResourcePulse.Services.Ping;

public sealed class UpdatePingDtoValidator : AbstractValidator<UpdatePingDto>
{
    public UpdatePingDtoValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(500);
    }
}
