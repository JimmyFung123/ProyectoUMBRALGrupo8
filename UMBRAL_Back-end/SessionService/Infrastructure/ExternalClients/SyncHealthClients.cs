namespace SessionService.Infrastructure.ExternalClients;

using System.Text.Json;
using SessionService.Application.SyncHealth;

// HU-27 — HTTP adapters for the four downstream services. Each one is a typed
// HttpClient registered in Program.cs with the matching BaseAddress, and they
// all share the same error contract: GET returns null on failure, POST returns
// false. The aggregator handler treats those signals as "card not green".

internal static class SyncHealthJson
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
}

public class MissionServiceSyncClient : IMissionServiceSyncClient
{
    private readonly HttpClient _http;
    public MissionServiceSyncClient(HttpClient http) => _http = http;

    public async Task<MissionServiceSyncSnapshot?> GetSnapshotAsync(CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync("api/internal/sync-health", ct);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<MissionServiceSyncSnapshot>(json, SyncHealthJson.Options);
        }
        catch { return null; }
    }

    public async Task<IReadOnlyList<UpstreamMissionRow>> GetMissionsAsync(CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync("api/missions", ct);
            if (!response.IsSuccessStatusCode) return [];
            var json = await response.Content.ReadAsStringAsync(ct);
            var rows = JsonSerializer.Deserialize<List<MissionRowJson>>(json, SyncHealthJson.Options) ?? [];
            return rows.Select(r => new UpstreamMissionRow(r.Id, r.Name, r.Status)).ToList();
        }
        catch { return []; }
    }

    public async Task<bool> ReprojectStageCountLookupAsync(CancellationToken ct)
    {
        try
        {
            var response = await _http.PostAsync("api/internal/sync-health/reproject", content: null, ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private record MissionRowJson(Guid Id, string Name, string Status);
}

public class StageServiceSyncClient : IStageServiceSyncClient
{
    private readonly HttpClient _http;
    public StageServiceSyncClient(HttpClient http) => _http = http;

    public async Task<StageServiceSyncSnapshot?> GetSnapshotAsync(CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync("api/internal/sync-health", ct);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<StageServiceSyncSnapshot>(json, SyncHealthJson.Options);
        }
        catch { return null; }
    }

    public async Task<bool> ReprojectMissionsLookupAsync(CancellationToken ct)
    {
        try
        {
            var response = await _http.PostAsync("api/internal/sync-health/reproject", content: null, ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}

public class ClueServiceSyncClient : IClueServiceSyncClient
{
    private readonly HttpClient _http;
    public ClueServiceSyncClient(HttpClient http) => _http = http;

    public async Task<ClueServiceSyncSnapshot?> GetSnapshotAsync(CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync("api/internal/sync-health", ct);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<ClueServiceSyncSnapshot>(json, SyncHealthJson.Options);
        }
        catch { return null; }
    }

    public async Task<bool> ReprojectStageLookupAsync(CancellationToken ct)
    {
        try
        {
            var response = await _http.PostAsync("api/internal/sync-health/reproject", content: null, ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}

public class TeamServiceSyncClient : ITeamServiceSyncClient
{
    private readonly HttpClient _http;
    public TeamServiceSyncClient(HttpClient http) => _http = http;

    public async Task<TeamServiceSyncSnapshot?> GetSnapshotAsync(CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync("api/internal/sync-health", ct);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<TeamServiceSyncSnapshot>(json, SyncHealthJson.Options);
        }
        catch { return null; }
    }

    public async Task<bool> ReprojectRankingForSessionAsync(Guid sessionId, CancellationToken ct)
    {
        try
        {
            var response = await _http.PostAsync(
                $"api/internal/sync-health/ranking/{sessionId}/reproject",
                content: null,
                ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
