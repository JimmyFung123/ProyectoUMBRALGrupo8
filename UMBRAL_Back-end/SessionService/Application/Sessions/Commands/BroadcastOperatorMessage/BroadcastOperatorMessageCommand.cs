namespace SessionService.Application.Sessions.Commands.BroadcastOperatorMessage;

using MediatR;
using SessionService.Domain.Common;

/// <summary>
/// HU-28 — operator-to-participants live message.
///
/// The operator types a short text from the dashboard, the command pushes it
/// through SignalR to every participant connected to the session group, and
/// the message is also written to the audit log so the action stays traceable.
/// </summary>
public record BroadcastOperatorMessageCommand(
    Guid SessionId,
    string Message,
    string? OperatorName) : IRequest<Result<BroadcastOperatorMessageResultDto>>;

public record BroadcastOperatorMessageResultDto(
    Guid SessionId,
    string Message,
    DateTime DeliveredAt);
