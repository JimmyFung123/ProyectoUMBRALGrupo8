namespace UMBRAL_Back_end.Application.Missions.Commands.RemoveClue;

using MediatR;
using UMBRAL_Back_end.Domain.Common;

public record RemoveClueCommand(Guid MissionId, Guid StageId, Guid ClueId) : IRequest<Result>;
