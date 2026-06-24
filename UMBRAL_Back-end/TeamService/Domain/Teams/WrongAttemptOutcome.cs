namespace TeamService.Domain.Teams;

/// <summary>
/// Result of recording a wrong trivia attempt on the <see cref="Team"/> aggregate.
/// The stage is NOT advanced — the team stays on the same stage.
/// </summary>
/// <param name="NewWrongCount">Total wrong attempts on the current stage after this one.</param>
/// <param name="NewScore">Team score after applying the penalty.</param>
public record WrongAttemptOutcome(int NewWrongCount, int NewScore);
