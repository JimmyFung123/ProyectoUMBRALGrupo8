namespace StageService.Application.Stages;

using StageService.Application.Stages.Queries.GetStagesByMission;
using StageService.Domain.Stages;

public static class StageMapper
{
    public static StageDto ToDto(Stage stage) => new(
        stage.Id,
        stage.MissionId,
        stage.Title,
        stage.Type.ToString(),
        stage.Order,
        stage.BaseScore,
        stage.Question,
        stage.Options.Select(o => new TriviaOptionDto(o.Id, o.Text, o.IsCorrect)).ToList(),
        stage.Latitude,
        stage.Longitude,
        stage.QrCode,
        stage.AutoReleaseTimeMinutes,
        stage.AutoReleaseMaxAttempts,
        stage.CreatedAt);
}
