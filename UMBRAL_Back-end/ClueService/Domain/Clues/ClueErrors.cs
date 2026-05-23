namespace ClueService.Domain.Clues;
using ClueService.Domain.Common;
public static class ClueErrors
{
    public static readonly Error NotFound = new("Clue.NotFound", "Clue not found.");
    public static readonly Error StageNotFound = new("Clue.StageNotFound", "Stage not found in lookup.");
    public static readonly Error InvalidContent = new("Clue.InvalidContent", "Clue content cannot be empty.");
}
