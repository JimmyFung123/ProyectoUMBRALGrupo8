namespace SessionService.Infrastructure.ExternalClients;

using System.Text.Json;
using SessionService.Application.Sessions;

public class ClueServiceClient : IClueServiceClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public ClueServiceClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<ClueInfo>> GetCluesByStageAsync(Guid stageId, CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync($"api/clues?stageId={stageId}", ct);
            if (!response.IsSuccessStatusCode) return [];
            var json = await response.Content.ReadAsStringAsync(ct);
            var items = JsonSerializer.Deserialize<List<ClueJsonItem>>(json, _jsonOptions) ?? [];
            return items.Select(x => new ClueInfo(x.Id, x.Content, x.Order, x.AutoReleaseAfterMinutes))
                        .OrderBy(c => c.Order)
                        .ToList();
        }
        catch { return []; }
    }

    private record ClueJsonItem(Guid Id, string Content, int Order, int? AutoReleaseAfterMinutes);
}
