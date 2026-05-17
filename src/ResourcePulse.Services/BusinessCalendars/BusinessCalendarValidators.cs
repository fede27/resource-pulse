using FluentValidation;
using ResourcePulse.Services.Shared;

namespace ResourcePulse.Services.BusinessCalendars;

public sealed class CreateBusinessCalendarDtoValidator : AbstractValidator<CreateBusinessCalendarDto>
{
    public CreateBusinessCalendarDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleForEach(x => x.Windows).SetValidator(new WorkWindowDtoValidator());
    }
}

public sealed class UpdateBusinessCalendarDtoValidator : AbstractValidator<UpdateBusinessCalendarDto>
{
    public UpdateBusinessCalendarDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}
