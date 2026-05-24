namespace SessionService.Infrastructure.ExternalClients;

using System.Text.Json;
using SessionService.Application.Sessions;

/// <summary>
/// HTTP adapter that queries TeamService to check team enrollment and manage clue releases.
/// </summary>
public class TeamServiceClient : ITeamServiceClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public TeamServiceClient(HttpClient http) => _http = http;

    public async Task<bool> HasEnrolledTeamsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _http.GetAsync(
                $"api/teams?sessionId={sessionId}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.ValueKind == JsonValueKind.Array
                && doc.RootElement.GetArrayLength() > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<int> ReleaseClueAsync(Guid teamId, int totalCluesForStage, CancellationToken cancellationToken, bool isAutomatic = false)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { totalCluesForStage, isAutomatic });
            var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            var response = await _http.PostAsync(
                $"api/teams/{teamId}/release-clue",
                content,
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                return -1; // all clues exhausted

            if (!response.IsSuccessStatusCode)
                return -1;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("cluesReceived").GetInt32();
        }
        catch
        {
            return -1;
        }
    }

    public async Task<IReadOnlyList<TeamProgressItem>> GetTeamProgressAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _http.GetAsync($"api/teams?sessionId={sessionId}", cancellationToken);
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var items = JsonSerializer.Deserialize<List<TeamProgressJsonItem>>(json, _jsonOptions) ?? [];
            return items.Select(x => new TeamProgressItem(
                x.Id,
                x.Name,
                x.CurrentStageOrder,
                x.CluesReceivedCurrentStage,
                x.ClueTimerResetAt,
                x.LastClueWasAutomatic)).ToList();
        }
        catch
        {
            return [];
        }
    }

    private record TeamProgressJsonItem(
        Guid Id,
        string Name,
        bool IsConnected,
        int CurrentStageOrder,
        int CluesReceivedCurrentStage,
        int Score,
        int Rank,
        DateTime? ClueTimerResetAt,
        bool LastClueWasAutomatic);
}
