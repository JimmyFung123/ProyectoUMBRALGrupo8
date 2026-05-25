namespace SessionService.Application.Sessions.Commands.ValidateQrCode;

using MediatR;
using SessionService.Domain.Common;

public record ValidateQrCodeCommand(
    Guid SessionId,
    Guid TeamId,
    Guid StageId,
    string ScannedCode) : IRequest<Result<QrValidationResultDto>>;
