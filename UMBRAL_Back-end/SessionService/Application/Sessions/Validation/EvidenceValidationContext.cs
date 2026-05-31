namespace SessionService.Application.Sessions.Validation;

using SessionService.Application.Sessions;
using SessionService.Domain.Sessions;

/// <summary>
/// Data passed through the Chain of Responsibility when validating evidence
/// submissions (trivia answers and QR scans) before processing begins.
/// </summary>
public record EvidenceValidationContext(
    Session? Session,
    StageWithOptionsInfo? Stage);
