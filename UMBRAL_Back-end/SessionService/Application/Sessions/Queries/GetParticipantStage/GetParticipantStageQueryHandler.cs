namespace SessionService.Application.Sessions.Queries.GetParticipantStage;

using MediatR;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

public class GetParticipantStageQueryHandler : IRequestHandler<GetParticipantStageQuery, Result<ParticipantStageDto>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamServiceClient _teamClient;
    private readonly IStageServiceClient _stageClient;

    public GetParticipantStageQueryHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient,
        IStageServiceClient stageClient)
    {
        _sessionRepository = sessionRepository;
        _teamClient = teamClient;
        _stageClient = stageClient;
    }

    public async Task<Result<ParticipantStageDto>> Handle(GetParticipantStageQuery request, CancellationToken cancellationToken)
    {
        // 1. Load session
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<ParticipantStageDto>(SessionErrors.NotFound);

        // 2. Get team's current progress
        var team = await _teamClient.GetTeamByIdAsync(request.TeamId, cancellationToken);
        if (team is null)
            return Result.Failure<ParticipantStageDto>(SessionErrors.TeamNotFound);

        var currentStageOrder = team.CurrentStageOrder;

        // 3. Auto-start: session is InProgress but team hasn't been assigned a stage yet
        if (session.Status == SessionStatus.InProgress && currentStageOrder == 0)
        {
            await _teamClient.ForceAdvanceTeamAsync(request.TeamId, 1, cancellationToken);
            currentStageOrder = 1;
        }

        // 4. Get all stages for the mission
        var stages = await _stageClient.GetStagesByMissionAsync(session.MissionId, cancellationToken);
        if (stages.Count == 0)
            return Result.Failure<ParticipantStageDto>(SessionErrors.NotFound);

        var maxOrder = stages.Max(s => s.Order);

        // 5. Team not yet started or session still pending
        if (currentStageOrder == 0)
        {
            return Result.Success(new ParticipantStageDto(
                Guid.Empty,
                "Waiting",
                "Waiting",
                0,
                null,
                [],
                session.Status.ToString(),
                0,
                false));
        }

        // 6. Team has finished all stages (sentinel: currentStageOrder > maxOrder)
        if (currentStageOrder > maxOrder)
        {
            return Result.Success(new ParticipantStageDto(
                Guid.Empty,
                "Completed",
                "Completed",
                currentStageOrder,
                null,
                [],
                session.Status.ToString(),
                currentStageOrder,
                true));
        }

        // 7. Find the current stage record by order
        var stageRef = stages.FirstOrDefault(s => s.Order == currentStageOrder);
        if (stageRef is null)
            return Result.Failure<ParticipantStageDto>(SessionErrors.NotFound);

        // 8. Fetch full stage details with options
        var stageDetails = await _stageClient.GetStageWithOptionsAsync(stageRef.Id, cancellationToken);
        if (stageDetails is null)
            return Result.Failure<ParticipantStageDto>(SessionErrors.NotFound);

        // 9. Strip IsCorrect — participants only get Id + Text
        var options = stageDetails.Options
            .Select(o => new ParticipantOptionDto(o.Id, o.Text))
            .ToList();

        bool isLastStage = currentStageOrder == maxOrder;

        return Result.Success(new ParticipantStageDto(
            stageDetails.Id,
            stageDetails.Title,
            stageDetails.Type,
            stageDetails.Order,
            stageDetails.Question,
            options,
            session.Status.ToString(),
            currentStageOrder,
            isLastStage));
    }
}
