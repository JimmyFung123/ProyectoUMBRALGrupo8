namespace StageService.Application.Stages.Commands.SetAutoRelease;
using MediatR;
using StageService.Domain.Common;

public record SetAutoReleaseCommand(Guid StageId, int? TimeMinutes, int? MaxAttempts) : IRequest<Result<bool>>;
