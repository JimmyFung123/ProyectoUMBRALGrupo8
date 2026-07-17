namespace StageService.Domain.Stages;

using StageService.Domain.Common;

/// <summary>
/// Value Object for the physical QR code identifier of a TreasureHunt stage
/// (Mission Design context, RB-20). The QR scan is the sole way to complete a
/// treasure stage (HU-19), so its presence is mandatory and it is trimmed for
/// consistent matching.
///
/// Named <c>QrCodeValue</c> (not <c>QrCode</c>) to avoid colliding with the
/// <see cref="Stage.QrCode"/> property, which stays a primitive string for the
/// EF Core / DTO / frontend contract.
/// </summary>
public sealed record QrCodeValue
{
    public string Value { get; }

    private QrCodeValue(string value) => Value = value;

    public static Result<QrCodeValue> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Result.Failure<QrCodeValue>(StageErrors.QrCodeRequired);

        return Result.Success(new QrCodeValue(raw.Trim()));
    }

    public override string ToString() => Value;
}
