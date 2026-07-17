namespace SessionService.Application.Missions.Composite;

/// <summary>
/// VISITOR PATTERN (companion to Composite). Lets new operations be added over
/// the mission tree (summaries, validation, export…) without modifying the
/// component classes. Each component's <c>Accept</c> double-dispatches to the
/// matching overload here.
/// </summary>
public interface IMissionComponentVisitor
{
    void Visit(MissionComponent mission);
    void Visit(StageComponent stage);
    void Visit(ClueComponent clue);
}
