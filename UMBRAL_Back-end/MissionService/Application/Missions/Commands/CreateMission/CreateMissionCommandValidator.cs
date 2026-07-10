namespace UMBRAL_Back_end.Application.Missions.Commands.CreateMission;

using FluentValidation;
using UMBRAL_Back_end.Domain.Missions;

/// <summary>
/// Fail-fast input validation, run by ValidationBehavior BEFORE the handler
/// touches the repository. Deliberately narrow — Name/Description/MaxDuration
/// are already guaranteed correct by their value objects (MissionName,
/// MissionDescription, SessionDuration) inside Mission.Create; duplicating
/// those rules here would just create a second source of truth. Difficulty has
/// no value object today and is only parsed deep inside the handler (after an
/// ExistsWithNameAsync round-trip), so catching it here avoids a wasted DB call
/// on obviously malformed input.
/// </summary>
public class CreateMissionCommandValidator : AbstractValidator<CreateMissionCommand>
{
    public CreateMissionCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la misión es obligatorio.");

        RuleFor(x => x.Difficulty)
            .Must(d => Enum.TryParse<DifficultyLevel>(d, ignoreCase: true, out _))
            .WithMessage("Difficulty debe ser 'Easy', 'Medium' o 'Hard'.");

        RuleFor(x => x.MaxDuration)
            .GreaterThan(0).WithMessage("MaxDuration debe ser mayor a cero.");
    }
}
