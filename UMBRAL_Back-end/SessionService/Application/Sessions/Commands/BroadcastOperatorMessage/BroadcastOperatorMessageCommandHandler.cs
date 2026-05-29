namespace SessionService.Application.Sessions.Commands.BroadcastOperatorMessage;

using MediatR;
using Microsoft.AspNetCore.SignalR;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using SessionService.Infrastructure.Hubs;

/// <summary>
/// HU-28 — pushes an operator-authored message to every participant connected
/// to the session group and writes the action to the audit log.
///
/// Allowed only while the session is InProgress or Paused: outside that window
/// the participants either haven't connected yet (Pending) or shouldn't be
/// reacting to anything (Completed/Cancelled).
/// </summary>
public class BroadcastOperatorMessageCommandHandler
    : IRequestHandler<BroadcastOperatorMessageCommand, Result<BroadcastOperatorMessageResultDto>>
{
    private const int MaxMessageLength = 240;

    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionEventRepository _eventRepository;
    private readonly IHubContext<SessionHub> _hub;

    public BroadcastOperatorMessageCommandHandler(
        ISessionRepository sessionRepository,
        ISessionEventRepository eventRepository,
        IHubContext<SessionHub> hub)
    {
        _sessionRepository = sessionRepository;
        _eventRepository = eventRepository;
        _hub = hub;
    }

    public async Task<Result<BroadcastOperatorMessageResultDto>> Handle(
        BroadcastOperatorMessageCommand request,
        CancellationToken cancellationToken)
    {
        var trimmed = (request.Message ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
            return Result.Failure<BroadcastOperatorMessageResultDto>(SessionErrors.EmptyBroadcastMessage);

        if (trimmed.Length > MaxMessageLength)
            return Result.Failure<BroadcastOperatorMessageResultDto>(SessionErrors.BroadcastMessageTooLong);

        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<BroadcastOperatorMessageResultDto>(SessionErrors.NotFound);

        if (session.Status != SessionStatus.InProgress && session.Status != SessionStatus.Paused)
            return Result.Failure<BroadcastOperatorMessageResultDto>(SessionErrors.CannotBroadcastMessage);

        var deliveredAt = DateTime.UtcNow;
        var actor = string.IsNullOrWhiteSpace(request.OperatorName)
            ? SessionEvent.SystemActor
            : request.OperatorName!.Trim();

        var audit = SessionEvent.Create(
            request.SessionId,
            description: $"Mensaje del operador enviado a participantes: \"{trimmed}\".",
            actorName: actor,
            commandType: nameof(BroadcastOperatorMessageCommand),
            outcome: SessionEvent.OutcomeSuccess);
        await _eventRepository.AddAsync(audit, cancellationToken);
        await _eventRepository.SaveChangesAsync(cancellationToken);

        // Push to participants. Payload carries the actor so the toast can
        // show "Prof. Ortega says: …" in real time.
        await _hub.Clients
            .Group(request.SessionId.ToString())
            .SendAsync(
                "OperatorMessage",
                new
                {
                    SessionId  = request.SessionId,
                    Message    = trimmed,
                    ActorName  = actor,
                    DeliveredAt = deliveredAt,
                },
                cancellationToken);

        // Trigger dashboard refresh so the operator's audit timeline updates too.
        await _hub.Clients
            .Group(request.SessionId.ToString())
            .SendAsync("SessionStateChanged", cancellationToken);

        return Result.Success(new BroadcastOperatorMessageResultDto(
            SessionId: request.SessionId,
            Message: trimmed,
            DeliveredAt: deliveredAt));
    }
}
