namespace ClueService.Domain.Clues;
using ClueService.Domain.Common;
public static class ClueErrors
{
    public static readonly Error NotFound = new("Clue.NotFound", "Clue not found.");
    public static readonly Error StageNotFound = new("Clue.StageNotFound", "Stage not found in lookup.");
    public static readonly Error InvalidContent = new("Clue.InvalidContent", "Clue content cannot be empty for a Trivia clue.");
    public static readonly Error InvalidGeoData = new("Clue.InvalidGeoData", "TreasureHunt clues require valid latitude, longitude and a positive radius (in meters).");
    public static readonly Error InvalidOrder = new("Clue.InvalidOrder", "Clue order must be a positive integer.");
    public static readonly Error StageTypeMismatch = new("Clue.StageTypeMismatch", "Clue payload does not match the stage type (Trivia expects text, TreasureHunt expects geo).");
}
