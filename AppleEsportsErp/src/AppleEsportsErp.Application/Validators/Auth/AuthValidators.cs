using FluentValidation;
using AppleEsportsErp.Application.DTOs.Auth;

namespace AppleEsportsErp.Application.Validators.Auth;

/// <summary>SOP §6.2: Super Admin Login validation</summary>
public class AdminLoginValidator : AbstractValidator<AdminLoginDto>
{
    public AdminLoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Valid email is required")
            .EmailAddress().WithMessage("Valid email is required");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters");
    }
}

/// <summary>SOP §6.3: Operator Login validation</summary>
public class OperatorLoginValidator : AbstractValidator<OperatorLoginDto>
{
    public OperatorLoginValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Valid branch ID is required");

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password or PIN is required");
    }
}

public class RefreshTokenValidator : AbstractValidator<RefreshTokenDto>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required");
    }
}
