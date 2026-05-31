namespace SessionService.Application.Sessions.Queries.GetParticipantStage;

using MediatR;
using SessionService.Application.Sessions.Facade;
using SessionService.Domain.Common;

public class GetParticipantStageQueryHandler
    : IRequestHandler<GetParticipantStageQuery, Result<ParticipantStageDto>>
{
    private readonly IParticipantStageFacade _facade;

    public GetParticipantStageQueryHandler(IParticipantStageFacade facade)
        => _facade = facade;

    // Toda la orquestación (sesión + equipo + etapa + ocultar IsCorrect) vive
    // ahora detrás de la fachada; el handler solo delega.
    public Task<Result<ParticipantStageDto>> Handle(
        GetParticipantStageQuery request,
        CancellationToken cancellationToken)
        => _facade.GetCurrentStageAsync(request.SessionId, request.TeamId, cancellationToken);
}
