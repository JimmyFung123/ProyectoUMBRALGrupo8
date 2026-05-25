namespace SessionService.Application.Sessions.Commands.ValidateQrCode;

using MediatR;
using SessionService.Domain.Common;

/// <summary>
/// QR validation comes from the participant — there is no operator involved,
/// so the audit entry is attributed to the team itself (HU-22).
/// </summary>
public record ValidateQrCodeCommand(
    Guid SessionId,
    Guid TeamId,
    Guid StageId,
    string ScannedCode) : IRequest<Result<QrValidationResultDto>>;
