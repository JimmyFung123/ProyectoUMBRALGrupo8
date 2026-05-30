namespace TeamService.Application.Teams.Commands.CreateTeam;
using MediatR;
using TeamService.Application.Rankings;
using TeamService.Domain.Common;
using TeamService.Domain.Teams;

public class CreateTeamCommandHandler : IRequestHandler<CreateTeamCommand, Result<CreateTeamResult>>
{
    private readonly ITeamRepository _repo;
    private readonly IRankingProjector _rankingProjector;

    public CreateTeamCommandHandler(ITeamRepository repo, IRankingProjector rankingProjector)
    {
        _repo = repo;
        _rankingProjector = rankingProjector;
    }

    public async Task<Result<CreateTeamResult>> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        // HU-17: validate the participant-supplied team name via the TeamName VO.
        var nameResult = TeamName.Create(request.TeamName);
        if (nameResult.IsFailure)
            return Result.Failure<CreateTeamResult>(nameResult.Error);

        var team = Team.Create(request.SessionId, nameResult.Value.Value);
        await _repo.AddAsync(team, cancellationToken);

        // HU-24: keep the ranking read model in sync within the same transaction.
        await _rankingProjector.RebuildAsync(team.SessionId, cancellationToken);

        await _repo.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateTeamResult(team.Id, team.InviteCode));
    }
}
