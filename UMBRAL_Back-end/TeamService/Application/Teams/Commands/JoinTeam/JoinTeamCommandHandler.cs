namespace TeamService.Application.Teams.Commands.JoinTeam;
using MediatR;
using TeamService.Domain.Common;
using TeamService.Domain.Teams;

public class JoinTeamCommandHandler : IRequestHandler<JoinTeamCommand, Result<JoinTeamResult>>
{
    private readonly ITeamRepository _repo;
    public JoinTeamCommandHandler(ITeamRepository repo) => _repo = repo;

    public async Task<Result<JoinTeamResult>> Handle(JoinTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _repo.GetByInviteCodeAsync(request.InviteCode, cancellationToken);
        if (team is null)
            return Result.Failure<JoinTeamResult>(TeamErrors.TeamNotFound);

        // El dominio decide si hay cupo (TeamMembershipPolicy vía Team.Join)
        var joinResult = team.Join();
        if (joinResult.IsFailure)
            return Result.Failure<JoinTeamResult>(joinResult.Error);

        await _repo.SaveChangesAsync(cancellationToken);

        return Result.Success(new JoinTeamResult(team.Id, team.Name, team.InviteCode, team.MemberCount));
    }
}
