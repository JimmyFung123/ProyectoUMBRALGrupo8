namespace TeamService.Application.Teams.Commands.CreateTeam;
using MediatR;
using TeamService.Domain.Common;
using TeamService.Domain.Teams;

public class CreateTeamCommandHandler : IRequestHandler<CreateTeamCommand, Result<CreateTeamResult>>
{
    private readonly ITeamRepository _repo;
    public CreateTeamCommandHandler(ITeamRepository repo) => _repo = repo;

    public async Task<Result<CreateTeamResult>> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TeamName))
            return Result.Failure<CreateTeamResult>(TeamErrors.InvalidTeamName);

        var team = Team.Create(request.SessionId, request.TeamName.Trim());
        await _repo.AddAsync(team, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateTeamResult(team.Id, team.InviteCode));
    }
}
