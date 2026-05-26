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
        if (string.IsNullOrWhiteSpace(request.TeamName))
            return Result.Failure<CreateTeamResult>(TeamErrors.InvalidTeamName);

        var team = Team.Create(request.SessionId, request.TeamName.Trim());
        await _repo.AddAsync(team, cancellationToken);

        // HU-24: keep the ranking read model in sync within the same transaction.
        await _rankingProjector.RebuildAsync(team.SessionId, cancellationToken);

        await _repo.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateTeamResult(team.Id, team.InviteCode));
    }
}
