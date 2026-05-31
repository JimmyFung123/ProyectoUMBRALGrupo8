namespace UMBRAL_Back_end.Infrastructure.ExternalClients;

using System.Text.Json;
using UMBRAL_Back_end.Application.Missions;

/// <summary>
/// HTTP adapter that queries SessionService to check for active sessions.
/// Calls GET /api/internal/sessions/has-active?missionId={id}.
/// </summary>
public class SessionServiceClient : ISessionServiceClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public SessionServiceClient(HttpClient http) => _http = http;

    public async Task<bool> HasActiveSessionsAsync(Guid missionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync(
                $"api/internal/sessions/has-active?missionId={missionId}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("hasActiveSessions").GetBoolean();
        }
        catch
        {
            // If SessionService is unreachable, fail-safe: block deactivation
            // to avoid orphaning active sessions.
            return true;
        }
    }
}
