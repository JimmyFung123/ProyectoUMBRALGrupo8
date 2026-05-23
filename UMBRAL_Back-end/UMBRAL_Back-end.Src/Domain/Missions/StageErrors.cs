namespace UMBRAL_Back_end.Domain.Missions;

using UMBRAL_Back_end.Domain.Common;

public static class StageErrors
{
    public static readonly Error InvalidTitle =
        new("Stage.InvalidTitle", "Stage title cannot be empty.");

    public static readonly Error InvalidOrder =
        new("Stage.InvalidOrder", "Stage order must be greater than zero.");

    public static readonly Error InvalidBaseScore =
        new("Stage.InvalidBaseScore", "Base score cannot be negative.");

    /// <summary>RB-20: TreasureHunt requires both latitude and longitude.</summary>
    public static readonly Error CoordinatesRequired =
        new("Stage.CoordinatesRequired", "Geographic coordinates (latitude and longitude) are required for TreasureHunt stages.");

    /// <summary>RB-20: TreasureHunt requires a QR code.</summary>
    public static readonly Error QrCodeRequired =
        new("Stage.QrCodeRequired", "A QR code is required for TreasureHunt stages.");

    /// <summary>Uniqueness constraint on QR codes across all stages.</summary>
    public static readonly Error DuplicateQrCode =
        new("Stage.DuplicateQrCode", "This QR code is already assigned to another stage.");

    public static readonly Error NotFound =
        new("Stage.NotFound", "Stage not found within this mission.");

    /// <summary>RB-14: Blocks edits when mission is Active or has sessions in progress.</summary>
    public static readonly Error MissionLockedForEditing =
        new("Stage.MissionLocked", "Cannot add, edit, or remove stages from a mission that is Active or has sessions in progress.");

    /// <summary>Two stages within the same mission cannot share the same Order value.</summary>
    public static readonly Error DuplicateStageOrder =
        new("Stage.DuplicateStageOrder", "Another stage with the same order already exists in this mission.");

    /// <summary>A Trivia stage must have at least two options.</summary>
    public static readonly Error TriviaRequiresAtLeastTwoOptions =
        new("Stage.TriviaRequiresAtLeastTwoOptions", "A Trivia stage must have at least two options.");

    /// <summary>A Trivia stage must have exactly one correct option.</summary>
    public static readonly Error TriviaRequiresExactlyOneCorrectOption =
        new("Stage.TriviaRequiresExactlyOneCorrectOption", "A Trivia stage must have exactly one correct option.");

    public static readonly Error InvalidAutoReleaseTime =
        new("Stage.InvalidAutoReleaseTime", "Auto-release time must be at least 1 minute.");

    public static readonly Error InvalidAutoReleaseAttempts =
        new("Stage.InvalidAutoReleaseAttempts", "Auto-release max attempts must be at least 1.");
}
