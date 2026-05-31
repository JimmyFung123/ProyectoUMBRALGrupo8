namespace StageService.Domain.Stages;
using StageService.Domain.Common;
public static class StageErrors
{
    public static readonly Error NotFound = new("Stage.NotFound", "Stage not found.");

    /// <summary>RB-14: stages cannot be modified while the mission is Active.</summary>
    public static readonly Error MissionIsActive = new("Stage.MissionIsActive", "Cannot modify stages of an active mission.");

    public static readonly Error InvalidName = new("Stage.InvalidName", "Stage name cannot be empty.");

    /// <summary>StageOrder VO: order must be a positive integer.</summary>
    public static readonly Error InvalidOrder = new("Stage.InvalidOrder", "Stage order must be greater than zero.");

    /// <summary>ScoreValue VO: base score cannot be negative.</summary>
    public static readonly Error InvalidBaseScore = new("Stage.InvalidBaseScore", "Base score cannot be negative.");

    /// <summary>RB-20: TreasureHunt stages require latitude and longitude.</summary>
    public static readonly Error CoordinatesRequired =
        new("Stage.CoordinatesRequired", "Geographic coordinates (latitude and longitude) are required for TreasureHunt stages.");

    /// <summary>GeoPoint VO: latitude/longitude out of valid range.</summary>
    public static readonly Error InvalidCoordinates =
        new("Stage.InvalidCoordinates", "Latitude must be between -90 and 90 and longitude between -180 and 180.");

    /// <summary>RB-20: TreasureHunt stages require a physical QR code for validation.</summary>
    public static readonly Error QrCodeRequired =
        new("Stage.QrCodeRequired", "A QR code is required for TreasureHunt stages.");

    /// <summary>HU-2: QR codes must be unique across all stages.</summary>
    public static readonly Error DuplicateQrCode =
        new("Stage.DuplicateQrCode", "This QR code is already assigned to another stage.");
}
