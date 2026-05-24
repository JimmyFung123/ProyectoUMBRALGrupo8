namespace TeamService.Application.Teams.Queries.GetTeamById;

public record TeamInfoDto(
    Guid TeamId,
    string TeamName,
    string InviteCode,
    int MemberCount,
    int CurrentStageOrder);
