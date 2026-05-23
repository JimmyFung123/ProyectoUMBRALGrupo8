namespace UMBRAL_Back_end.Domain.Missions;

using UMBRAL_Back_end.Domain.Common;

public static class ClueErrors
{
    public static readonly Error InvalidOrder =
        new("Clue.InvalidOrder", "Clue order must be greater than zero.");

    public static readonly Error NotFound =
        new("Clue.NotFound", "Clue not found within this stage.");

    public static readonly Error DuplicateClueOrder =
        new("Clue.DuplicateClueOrder", "Another clue with the same order already exists in this stage.");

    public static readonly Error ContentRequired =
        new("Clue.ContentRequired", "Content is required for Trivia clues.");

    public static readonly Error CoordinatesRequired =
        new("Clue.CoordinatesRequired", "Latitude and Longitude are required for TreasureHunt clues.");

    public static readonly Error RadiusRequired =
        new("Clue.RadiusRequired", "RadiusMeters is required for TreasureHunt clues.");

    /// <summary>RB-14: Blocks edits when mission is Active or has sessions in progress.</summary>
    public static readonly Error StageLocked =
        new("Clue.StageLocked", "Cannot modify clues of a mission that is Active or has sessions in progress.");
}
