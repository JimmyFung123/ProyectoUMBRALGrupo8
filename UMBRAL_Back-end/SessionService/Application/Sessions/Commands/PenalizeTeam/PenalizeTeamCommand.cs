namespace SessionService.Application.Sessions.Commands.PenalizeTeam;

using MediatR;
using SessionService.Domain.Common;

/// <summary>
/// Records a manual penalty applied by the operator (HU-15). <see cref="OperatorName"/>
/// is propagated to the SessionEvent for audit (HU-22, criterion 1) — falls back
/// to "Operador" when the front-end did not send the X-Operator-Name header.
/// </summary>
public record PenalizeTeamCommand(
    Guid SessionId,
    Guid TeamId,
    int Points,
    string Reason,
    string? OperatorName = null) : IRequest<Result<int>>;
