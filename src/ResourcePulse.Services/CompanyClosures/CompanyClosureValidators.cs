using FluentValidation;

namespace ResourcePulse.Services.CompanyClosures;

public sealed class CreateCompanyClosureDtoValidator : AbstractValidator<CreateCompanyClosureDto>
{
    public CreateCompanyClosureDtoValidator()
    {
        RuleFor(x => x.DateFrom).LessThanOrEqualTo(x => x.DateTo)
            .WithMessage("DateFrom must be on or before DateTo.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class UpdateCompanyClosureDtoValidator : AbstractValidator<UpdateCompanyClosureDto>
{
    public UpdateCompanyClosureDtoValidator()
    {
        RuleFor(x => x.DateFrom).LessThanOrEqualTo(x => x.DateTo)
            .WithMessage("DateFrom must be on or before DateTo.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
