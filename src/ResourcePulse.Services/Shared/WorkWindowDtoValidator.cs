using FluentValidation;

namespace ResourcePulse.Services.Shared;

public sealed class WorkWindowDtoValidator : AbstractValidator<WorkWindowDto>
{
    public WorkWindowDtoValidator()
    {
        RuleFor(w => w.DayOfWeek).IsInEnum();
        RuleFor(w => w.StartTime).LessThan(w => w.EndTime)
            .WithMessage("StartTime must be earlier than EndTime.");
        When(w => w.ValidTo is not null, () =>
        {
            RuleFor(w => w.ValidTo!.Value).GreaterThan(w => w.ValidFrom)
                .WithMessage("ValidTo must be later than ValidFrom.");
        });
    }
}
