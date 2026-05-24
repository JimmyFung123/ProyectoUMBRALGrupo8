namespace SessionService.Infrastructure.ExternalClients;

using System.Text.Json;
using SessionService.Application.Sessions;

/// <summary>
/// HTTP adapter that queries TeamService to check team enrollment.
/// Called only during session start to enforce the "at least 1 team" rule.
/// </summary>
public class TeamServiceClient : ITeamServiceClient
{
    private readonly HttpClient _http;

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

    public async Task<int> ReleaseClueAsync(Guid teamId, int totalCluesForStage, CancellationToken cancellationToken)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { totalCluesForStage });
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
}
