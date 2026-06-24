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

    public async Task<bool> AllTeamsMeetMinimumMembersAsync(Guid sessionId, int minMembers, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _http.GetAsync($"api/teams?sessionId={sessionId}", cancellationToken);
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var items = JsonSerializer.Deserialize<List<TeamProgressJsonItem>>(json, _jsonOptions);
            if (items is null || items.Count == 0) return false;

            return items.All(t => t.MemberCount >= minMembers);
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

    public async Task<int> PenalizeTeamAsync(Guid teamId, int points, string reason, CancellationToken cancellationToken)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { points, reason });
            var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            var response = await _http.PostAsync($"api/teams/{teamId}/penalize", content, cancellationToken);
            if (!response.IsSuccessStatusCode) return int.MinValue;
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("newScore").GetInt32();
        }
        catch { return int.MinValue; }
    }

    public async Task LeaveTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _http.PostAsync($"api/teams/{teamId}/leave", null, cancellationToken);
        }
        catch
        {
            // best-effort: the participant must be able to leave even if TeamService is unreachable.
        }
    }

    public async Task<StageTransitionResult?> ForceAdvanceTeamAsync(Guid teamId, int nextStageOrder, CancellationToken cancellationToken)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { nextStageOrder });
            var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            var response = await _http.PostAsync($"api/teams/{teamId}/force-advance", content, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            return new StageTransitionResult(
                NewScore: doc.RootElement.GetProperty("newScore").GetInt32(),
                ElapsedSeconds: doc.RootElement.GetProperty("elapsedSeconds").GetInt32());
        }
        catch { return null; }
    }

    public async Task<StageTransitionResult?> RecordEvidenceOutcomeAsync(Guid teamId, bool isCorrect, int scoreChange, int nextStageOrder, CancellationToken cancellationToken)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { isCorrect, scoreChange, nextStageOrder });
            var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            var response = await _http.PostAsync($"api/teams/{teamId}/record-evidence-outcome", content, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            return new StageTransitionResult(
                NewScore: doc.RootElement.GetProperty("newScore").GetInt32(),
                ElapsedSeconds: doc.RootElement.GetProperty("elapsedSeconds").GetInt32());
        }
        catch { return null; }
    }

    public async Task<TeamInfoItem?> GetTeamByIdAsync(Guid teamId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _http.GetAsync($"api/teams/{teamId}", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var item = JsonSerializer.Deserialize<TeamInfoJsonItem>(json, _jsonOptions);
            if (item is null) return null;
            return new TeamInfoItem(item.TeamId, item.TeamName, item.CurrentStageOrder);
        }
        catch { return null; }
    }

    public async Task<SessionRankingSnapshot?> GetSessionRankingAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _http.GetAsync(
                $"api/teams/ranking?sessionId={sessionId}",
                cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var snapshot = JsonSerializer.Deserialize<SessionRankingJsonSnapshot>(json, _jsonOptions);
            if (snapshot is null) return null;

            var teams = (snapshot.Teams ?? new List<SessionRankingTeamJsonItem>())
                .Select(t => new SessionRankingTeamItem(
                    t.TeamId,
                    t.Name,
                    t.Score,
                    t.Rank,
                    t.CurrentStageOrder,
                    t.IsConnected,
                    t.LastStageCompletedAt))
                .ToList();

            return new SessionRankingSnapshot(snapshot.SessionId, snapshot.GeneratedAt, teams);
        }
        catch { return null; }
    }

    private record SessionRankingJsonSnapshot(
        Guid SessionId,
        DateTime GeneratedAt,
        List<SessionRankingTeamJsonItem>? Teams);

    private record SessionRankingTeamJsonItem(
        Guid TeamId,
        string Name,
        int Score,
        int Rank,
        int CurrentStageOrder,
        bool IsConnected,
        DateTime? LastStageCompletedAt);

    private record TeamProgressJsonItem(
        Guid Id,
        string Name,
        bool IsConnected,
        int CurrentStageOrder,
        int CluesReceivedCurrentStage,
        int Score,
        int Rank,
        DateTime? ClueTimerResetAt,
        bool LastClueWasAutomatic,
        int MemberCount);

    private record TeamInfoJsonItem(
        Guid TeamId,
        string TeamName,
        string InviteCode,
        int MemberCount,
        int CurrentStageOrder);
}
