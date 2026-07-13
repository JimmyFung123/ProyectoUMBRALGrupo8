namespace SessionService.Application.Sessions.Commands.PenalizeTeam;

using FluentValidation;

/// <summary>
/// Reason was only guarded by a raw `if` inside the handler (collapsing into
/// the generic SessionErrors.CannotPenalizeTeam). Points wasn't validated at
/// all before the HTTP round-trip to TeamService — a bad value only surfaced
/// after the call, as the same generic error TeamService returns for "team
/// not found". Catching both here avoids the wasted network call and gives a
/// field-specific message instead of a one-size-fits-all error code.
/// </summary>
public class PenalizeTeamCommandValidator : AbstractValidator<PenalizeTeamCommand>
{
    public PenalizeTeamCommandValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("El motivo de la penalización es obligatorio.");

        RuleFor(x => x.Points)
            .GreaterThan(0).WithMessage("Points debe ser mayor a cero.");
    }
}
