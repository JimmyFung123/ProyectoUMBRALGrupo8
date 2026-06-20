namespace SessionService.Application.Sessions.Commands.BroadcastOperatorMessage;

using MediatR;
using SessionService.Application;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using UMBRAL.Contracts.Events;

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
    private readonly IIntegrationEventBus _bus;
    private readonly ISessionNotifier _notifier;

    public BroadcastOperatorMessageCommandHandler(
        ISessionRepository sessionRepository,
        IIntegrationEventBus bus,
        ISessionNotifier notifier)
    {
        _sessionRepository = sessionRepository;
        _bus = bus;
        _notifier = notifier;
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

        await _bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                request.SessionId,
                $"Mensaje del operador enviado a participantes: \"{trimmed}\".",
                ActorName: actor,
                CommandType: nameof(BroadcastOperatorMessageCommand),
                Outcome: SessionEvent.OutcomeSuccess,
                DateTime.UtcNow),
            cancellationToken);

        await _notifier.NotifyOperatorMessageAsync(
            request.SessionId, trimmed, actor, deliveredAt, cancellationToken);

        return Result.Success(new BroadcastOperatorMessageResultDto(
            SessionId: request.SessionId,
            Message: trimmed,
            DeliveredAt: deliveredAt));
    }
}
