namespace UMBRAL_Back_end.Application.Missions.Commands.UpdateMission;

using FluentValidation;
using UMBRAL_Back_end.Domain.Missions;

/// <summary>
/// Mirrors CreateMissionCommandValidator — UpdateMissionCommandHandler has the
/// exact same gap (Difficulty parsed via Enum.TryParse deep in the handler,
/// after an ExistsWithNameAsync round-trip). Same narrow scope: Name/Description
/// stay owned by MissionName/MissionDescription inside Mission.Update.
/// </summary>
public class UpdateMissionCommandValidator : AbstractValidator<UpdateMissionCommand>
{
    public UpdateMissionCommandValidator()
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
