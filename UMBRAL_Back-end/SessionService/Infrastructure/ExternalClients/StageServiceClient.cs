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

    private record StageJsonItem(Guid Id, int Order);
}
