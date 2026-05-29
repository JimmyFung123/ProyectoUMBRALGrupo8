namespace SessionService.Application.Sessions.Commands.SubmitTriviaAnswer;

using MediatR;
using Microsoft.AspNetCore.SignalR;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using SessionService.Domain.Statistics;
using SessionService.Infrastructure.Hubs;

public class SubmitTriviaAnswerCommandHandler : IRequestHandler<SubmitTriviaAnswerCommand, Result<TriviaAnswerResultDto>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamServiceClient _teamClient;
    private readonly IStageServiceClient _stageClient;
    private readonly IStageCompletionRecordRepository _statsRepository;
    private readonly ISessionEventRepository _eventRepository;
    private readonly IHubContext<SessionHub> _hub;

    public SubmitTriviaAnswerCommandHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient,
        IStageServiceClient stageClient,
        IStageCompletionRecordRepository statsRepository,
        ISessionEventRepository eventRepository,
        IHubContext<SessionHub> hub)
    {
        _sessionRepository = sessionRepository;
        _teamClient = teamClient;
        _stageClient = stageClient;
        _statsRepository = statsRepository;
        _eventRepository = eventRepository;
        _hub = hub;
    }

    public async Task<Result<TriviaAnswerResultDto>> Handle(SubmitTriviaAnswerCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate session exists and is in a state that accepts answers
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<TriviaAnswerResultDto>(SessionErrors.NotFound);

        if (session.Status != SessionStatus.InProgress)
            return Result.Failure<TriviaAnswerResultDto>(SessionErrors.NotInProgress);

        // 2. Fetch the stage with full option details (IsCorrect included — internal only)
        var stage = await _stageClient.GetStageWithOptionsAsync(request.StageId, cancellationToken);
        if (stage is null)
            return Result.Failure<TriviaAnswerResultDto>(SessionErrors.StageNotFound);

        // 3. Find the selected option
        var option = stage.Options.FirstOrDefault(o => o.Id == request.OptionId);
        if (option is null)
            return Result.Failure<TriviaAnswerResultDto>(SessionErrors.OptionNotFound);

        bool isCorrect = option.IsCorrect;

        // 4. Get all stages to determine next stage order and last-stage flag
        var allStages = await _stageClient.GetStagesByMissionAsync(session.MissionId, cancellationToken);
        var sortedStages = allStages.OrderBy(s => s.Order).ToList();
        var maxOrder = sortedStages.Max(s => s.Order);

        var currentStageIndex = sortedStages.FindIndex(s => s.Id == request.StageId);
        bool isLastStage = currentStageIndex == sortedStages.Count - 1;

        // Sentinel: one beyond max signals "team finished"
        int nextStageOrder = isLastStage
            ? maxOrder + 1
            : sortedStages[currentStageIndex + 1].Order;

        // 5. Record the answer in TeamService (updates score + advances stage)
        var outcome = await _teamClient.AnswerTriviaAsync(
            request.TeamId, isCorrect, stage.BaseScore, nextStageOrder, cancellationToken);

        if (outcome is null)
            return Result.Failure<TriviaAnswerResultDto>(SessionErrors.CannotAnswerTrivia);

        // 6. HU-25: append the analytics fact row. IncludedInStatistics stays
        // false until the session is finalized — admin dashboard ignores
        // in-flight games.
        var record = StageCompletionRecord.ForTriviaAnswer(
            sessionId: request.SessionId,
            missionId: session.MissionId,
            teamId: request.TeamId,
            stageOrder: stage.Order,
            elapsedSeconds: outcome.ElapsedSeconds,
            wasCorrect: isCorrect);
        await _statsRepository.AddAsync(record, cancellationToken);
        await _statsRepository.SaveChangesAsync(cancellationToken);

        // 7. HU-26: command audit log. Resolve team name for the actor field.
        var teamInfo = await _teamClient.GetTeamByIdAsync(request.TeamId, cancellationToken);
        var teamName = teamInfo?.Name ?? "Equipo";
        var auditMessage = isCorrect
            ? $"El equipo '{teamName}' respondió correctamente la etapa {stage.Order} y sumó {stage.BaseScore} pts. Nuevo puntaje: {outcome.NewScore}."
            : $"El equipo '{teamName}' respondió incorrectamente la etapa {stage.Order}. Nuevo puntaje: {outcome.NewScore}.";
        var auditEvent = SessionEvent.Create(
            request.SessionId,
            auditMessage,
            actorName: $"Equipo {teamName}",
            commandType: nameof(SubmitTriviaAnswerCommand),
            outcome: isCorrect ? SessionEvent.OutcomeSuccess : SessionEvent.OutcomeFailure);
        await _eventRepository.AddAsync(auditEvent, cancellationToken);
        await _eventRepository.SaveChangesAsync(cancellationToken);

        // 8. Broadcast dashboard refresh (operators) + HU-28 StageCompleted
        //    (participants). The dedicated event carries enough data for the
        //    immersive toast/animation to fire without waiting for the next
        //    polling tick.
        await _hub.Clients
            .Group(request.SessionId.ToString())
            .SendAsync("SessionStateChanged", cancellationToken);

        await _hub.Clients
            .Group(request.SessionId.ToString())
            .SendAsync(
                "StageCompleted",
                new
                {
                    SessionId       = request.SessionId,
                    TeamId          = request.TeamId,
                    StageOrder      = stage.Order,
                    StageType       = "Trivia",
                    WasCorrect      = isCorrect,
                    PointsEarned    = isCorrect ? stage.BaseScore : 0,
                    NewScore        = outcome.NewScore,
                    NextStageOrder  = nextStageOrder,
                    IsLastStage     = isLastStage,
                },
                cancellationToken);

        return Result.Success(new TriviaAnswerResultDto(isCorrect, outcome.NewScore, nextStageOrder, isLastStage));
    }
}
