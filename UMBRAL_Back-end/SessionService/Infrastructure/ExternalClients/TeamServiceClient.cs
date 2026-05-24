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

            // Response is an array — non-empty means at least one team is enrolled
            return doc.RootElement.ValueKind == JsonValueKind.Array
                && doc.RootElement.GetArrayLength() > 0;
        }
        catch
        {
            // If TeamService is unreachable, fail-safe: block the start
            return false;
        }
    }
}
