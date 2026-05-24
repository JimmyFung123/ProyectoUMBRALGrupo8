namespace SessionService.Infrastructure.ExternalClients;

using System.Text.Json;
using SessionService.Application.Sessions;

public class StageServiceClient : IStageServiceClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public StageServiceClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<StageInfo>> GetStagesByMissionAsync(Guid missionId, CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync($"api/stages?missionId={missionId}", ct);
            if (!response.IsSuccessStatusCode) return [];
            var json = await response.Content.ReadAsStringAsync(ct);
            var items = JsonSerializer.Deserialize<List<StageJsonItem>>(json, _jsonOptions) ?? [];
            return items.Select(x => new StageInfo(x.Id, x.Order)).ToList();
        }
        catch { return []; }
    }

    public async Task<StageWithOptionsInfo?> GetStageWithOptionsAsync(Guid stageId, CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync($"api/stages/{stageId}", ct);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync(ct);
            var item = JsonSerializer.Deserialize<StageWithOptionsJsonItem>(json, _jsonOptions);
            if (item is null) return null;

            var options = item.Options?
                .Select(o => new TriviaOptionInfo(o.Id, o.Text, o.IsCorrect))
                .ToList() ?? [];

            return new StageWithOptionsInfo(
                item.Id, item.Title, item.Type, item.Order, item.BaseScore,
                item.Question, options);
        }
        catch { return null; }
    }

    private record StageJsonItem(Guid Id, int Order);

    private record OptionJsonItem(Guid Id, string Text, bool IsCorrect);

    private record StageWithOptionsJsonItem(
        Guid Id,
        string Title,
        string Type,
        int Order,
        int BaseScore,
        string? Question,
        List<OptionJsonItem>? Options);
}
