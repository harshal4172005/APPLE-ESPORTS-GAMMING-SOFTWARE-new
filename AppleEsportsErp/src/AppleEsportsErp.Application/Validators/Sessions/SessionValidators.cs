using FluentValidation;
using AppleEsportsErp.Application.DTOs.Sessions;

namespace AppleEsportsErp.Application.Validators.Sessions;

public class SessionStartValidator : AbstractValidator<SessionStartDto>
{
    public SessionStartValidator()
    {
        RuleFor(x => x.PcId).NotEmpty().WithMessage("PC ID is required.");
        
        RuleFor(x => x)
            .Must(x => !string.IsNullOrEmpty(x.CustomerName) || x.MemberId.HasValue)
            .WithMessage("Either Customer Name or Member ID must be provided.");

        RuleFor(x => x.DurationMinutes)
            .GreaterThanOrEqualTo(0).WithMessage("Duration must be greater than or equal to 0 minutes.");

        RuleFor(x => x.PackageName).NotEmpty().WithMessage("Package Name is required.");
        RuleFor(x => x.ExpectedAmount).GreaterThanOrEqualTo(0).WithMessage("Expected amount cannot be negative.");
    }
}

public class SessionExtendValidator : AbstractValidator<SessionExtendDto>
{
    public SessionExtendValidator()
    {
        RuleFor(x => x.AdditionalMinutes).GreaterThan(0).WithMessage("Additional minutes must be greater than 0.");
        RuleFor(x => x.PackageName).NotEmpty().WithMessage("Package Name is required.");
        RuleFor(x => x.AdditionalAmount).GreaterThanOrEqualTo(0).WithMessage("Additional amount cannot be negative.");
    }
}

public class SessionTransferValidator : AbstractValidator<SessionTransferDto>
{
    public SessionTransferValidator()
    {
        RuleFor(x => x.TargetPcId).NotEmpty().WithMessage("Target PC ID is required.");
    }
}
