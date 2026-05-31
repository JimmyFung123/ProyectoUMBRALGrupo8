namespace UserService.Infrastructure.ExternalClients;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using UserService.Application.Users;
using UserService.Domain.Users;

/// <summary>
/// HTTP adapter for Keycloak's Admin REST API. Authenticates with the
/// <c>umbral-backend</c> confidential client (client_credentials grant)
/// and caches the access token in memory until it expires.
/// </summary>
public class KeycloakAdminClient : IKeycloakAdminClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _realm;
    private readonly string _clientId;
    private readonly string _clientSecret;

    private string? _cachedToken;
    private DateTime _cachedTokenExpiresAt = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // Keycloak (Jackson en Java) acepta JSON con propiedades en camelCase.
        // Sin esto, .NET serializa records como RealmRoleJson(Id, Name) como
        // {"Id":...,"Name":...} y Keycloak ignora silenciosamente el body —
        // POST /role-mappings/realm completa con 2xx pero NO asigna el rol,
        // dejando al usuario huérfano (lo que pasó con prueba@umbral.local).
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public KeycloakAdminClient(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _baseUrl = configuration["Keycloak:AdminBaseUrl"]?.TrimEnd('/')
                   ?? "http://localhost:18090";
        _realm        = configuration["Keycloak:Realm"]             ?? "umbral";
        _clientId     = configuration["Keycloak:AdminClientId"]     ?? "umbral-backend";
        _clientSecret = configuration["Keycloak:AdminClientSecret"] ?? string.Empty;
    }

    // ── Auth: cliente_credentials con cache en memoria ─────────────────────────

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        // Re-utiliza el token cacheado mientras le queden al menos 30s de vida.
        if (_cachedToken is not null && DateTime.UtcNow < _cachedTokenExpiresAt.AddSeconds(-30))
            return _cachedToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_cachedToken is not null && DateTime.UtcNow < _cachedTokenExpiresAt.AddSeconds(-30))
                return _cachedToken;

            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"]    = "client_credentials",
                ["client_id"]     = _clientId,
                ["client_secret"] = _clientSecret,
            });

            using var resp = await _http.PostAsync(
                $"{_baseUrl}/realms/{_realm}/protocol/openid-connect/token",
                form, ct);

            resp.EnsureSuccessStatusCode();
            var payload = await resp.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, ct)
                          ?? throw new InvalidOperationException("Empty token response");

            _cachedToken = payload.AccessToken;
            _cachedTokenExpiresAt = DateTime.UtcNow.AddSeconds(payload.ExpiresIn);
            return _cachedToken!;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<HttpRequestMessage> NewAuthedRequest(HttpMethod method, string relativeUrl, CancellationToken ct)
    {
        var token = await GetTokenAsync(ct);
        var msg = new HttpRequestMessage(method, $"{_baseUrl}/admin/realms/{_realm}{relativeUrl}");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return msg;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<KeycloakUser>> ListUsersAsync(CancellationToken cancellationToken)
    {
        using var req = await NewAuthedRequest(HttpMethod.Get,
            "/users?briefRepresentation=false&max=1000", cancellationToken);
        using var resp = await _http.SendAsync(req, cancellationToken);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<List<KeycloakUserJson>>(JsonOptions, cancellationToken)
                  ?? new List<KeycloakUserJson>();

        // Para resolver el rol de cada usuario hay que consultar /users/{id}/role-mappings/realm.
        // En vez de N llamadas (lento con muchos usuarios), pedimos los miembros de cada
        // rol UMBRAL de una vez y construimos un diccionario id → rol.
        var roleByUserId = await BuildRoleIndexAsync(cancellationToken);

        return raw
            .Select(u => new KeycloakUser(
                u.Id,
                u.Email ?? string.Empty,
                u.FirstName ?? string.Empty,
                u.LastName ?? string.Empty,
                u.Enabled ?? false,
                roleByUserId.TryGetValue(u.Id, out var r) ? r : null))
            .ToList();
    }

    private async Task<Dictionary<Guid, UserRole>> BuildRoleIndexAsync(CancellationToken ct)
    {
        var result = new Dictionary<Guid, UserRole>();
        foreach (var role in new[] { UserRole.Admin, UserRole.Operator })
        {
            using var req = await NewAuthedRequest(HttpMethod.Get,
                $"/roles/{role.ToKeycloakName()}/users?max=1000", ct);
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) continue;

            var members = await resp.Content.ReadFromJsonAsync<List<KeycloakUserJson>>(JsonOptions, ct)
                          ?? new List<KeycloakUserJson>();
            foreach (var m in members)
                result[m.Id] = role; // si está en admin y operator, admin gana (Admin se procesa primero)
        }
        return result;
    }

    public async Task<KeycloakUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var encoded = Uri.EscapeDataString(email.Trim());
        using var req = await NewAuthedRequest(HttpMethod.Get,
            $"/users?email={encoded}&exact=true", cancellationToken);
        using var resp = await _http.SendAsync(req, cancellationToken);
        if (!resp.IsSuccessStatusCode) return null;

        var users = await resp.Content.ReadFromJsonAsync<List<KeycloakUserJson>>(JsonOptions, cancellationToken)
                    ?? new List<KeycloakUserJson>();
        var match = users.FirstOrDefault();
        if (match is null) return null;

        var role = await GetUserRoleAsync(match.Id, cancellationToken);
        return new KeycloakUser(match.Id, match.Email ?? string.Empty,
                                match.FirstName ?? string.Empty, match.LastName ?? string.Empty,
                                match.Enabled ?? false, role);
    }

    public async Task<KeycloakUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var req = await NewAuthedRequest(HttpMethod.Get, $"/users/{userId}", cancellationToken);
        using var resp = await _http.SendAsync(req, cancellationToken);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();

        var u = await resp.Content.ReadFromJsonAsync<KeycloakUserJson>(JsonOptions, cancellationToken);
        if (u is null) return null;

        var role = await GetUserRoleAsync(u.Id, cancellationToken);
        return new KeycloakUser(u.Id, u.Email ?? string.Empty,
                                u.FirstName ?? string.Empty, u.LastName ?? string.Empty,
                                u.Enabled ?? false, role);
    }

    private async Task<UserRole?> GetUserRoleAsync(Guid userId, CancellationToken ct)
    {
        using var req = await NewAuthedRequest(HttpMethod.Get,
            $"/users/{userId}/role-mappings/realm", ct);
        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var roles = await resp.Content.ReadFromJsonAsync<List<RealmRoleJson>>(JsonOptions, ct)
                    ?? new List<RealmRoleJson>();

        // Admin gana si por alguna razón el usuario tiene ambos asignados.
        if (roles.Any(r => r.Name == "admin"))    return UserRole.Admin;
        if (roles.Any(r => r.Name == "operator")) return UserRole.Operator;
        return null;
    }

    public async Task<Guid> CreateUserAsync(
        string email, string firstName, string lastName,
        string temporaryPassword, UserRole role,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            email,
            username = email,
            firstName,
            lastName,
            enabled = true,
            emailVerified = true,
            credentials = new[]
            {
                new { type = "password", value = temporaryPassword, temporary = false },
            },
        };

        using var req = await NewAuthedRequest(HttpMethod.Post, "/users", cancellationToken);
        req.Content = JsonContent.Create(body, options: JsonOptions);
        using var resp = await _http.SendAsync(req, cancellationToken);

        if (resp.StatusCode == HttpStatusCode.Conflict)
            throw new KeycloakConflictException("Keycloak rejected the user as duplicate.");

        resp.EnsureSuccessStatusCode();

        // Keycloak returns the new user's URL in the Location header.
        var location = resp.Headers.Location?.ToString()
            ?? throw new InvalidOperationException("Keycloak did not return Location for new user.");
        var id = Guid.Parse(location[(location.LastIndexOf('/') + 1)..]);

        // Asignar el rol UMBRAL acto seguido. Si esto falla, el usuario queda
        // sin rol — el admin verá "Sin rol" en la UI y podrá corregir.
        await AssignRoleAsync(id, role, cancellationToken);
        return id;
    }

    public async Task ChangeRoleAsync(Guid userId, UserRole newRole, CancellationToken cancellationToken)
    {
        // 1) Asigna el rol nuevo PRIMERO. Si esto falla, el usuario nunca queda
        //    sin rol UMBRAL.
        await AssignRoleAsync(userId, newRole, cancellationToken);

        // 2) Quita el rol viejo (admin si el nuevo es operator, y viceversa).
        var oldRole = newRole == UserRole.Admin ? UserRole.Operator : UserRole.Admin;
        await UnassignRoleAsync(userId, oldRole, cancellationToken);
    }

    private async Task AssignRoleAsync(Guid userId, UserRole role, CancellationToken ct)
    {
        // Necesitamos el RoleRepresentation con id+name; lo obtenemos con un GET.
        var roleRep = await GetRealmRoleAsync(role.ToKeycloakName(), ct);
        if (roleRep is null) return;

        using var req = await NewAuthedRequest(HttpMethod.Post,
            $"/users/{userId}/role-mappings/realm", ct);
        req.Content = JsonContent.Create(new[] { roleRep }, options: JsonOptions);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    private async Task UnassignRoleAsync(Guid userId, UserRole role, CancellationToken ct)
    {
        var roleRep = await GetRealmRoleAsync(role.ToKeycloakName(), ct);
        if (roleRep is null) return;

        using var req = await NewAuthedRequest(HttpMethod.Delete,
            $"/users/{userId}/role-mappings/realm", ct);
        req.Content = JsonContent.Create(new[] { roleRep }, options: JsonOptions);
        using var resp = await _http.SendAsync(req, ct);
        // 404 significa que el rol ya no estaba — lo aceptamos.
        if (resp.StatusCode != HttpStatusCode.NotFound)
            resp.EnsureSuccessStatusCode();
    }

    private async Task<RealmRoleJson?> GetRealmRoleAsync(string roleName, CancellationToken ct)
    {
        using var req = await NewAuthedRequest(HttpMethod.Get, $"/roles/{roleName}", ct);
        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<RealmRoleJson>(JsonOptions, ct);
    }

    public Task DisableAsync(Guid userId, CancellationToken cancellationToken)
        => SetEnabledAsync(userId, enabled: false, cancellationToken);

    public Task EnableAsync(Guid userId, CancellationToken cancellationToken)
        => SetEnabledAsync(userId, enabled: true, cancellationToken);

    private async Task SetEnabledAsync(Guid userId, bool enabled, CancellationToken ct)
    {
        using var req = await NewAuthedRequest(HttpMethod.Put, $"/users/{userId}", ct);
        req.Content = JsonContent.Create(new { enabled }, options: JsonOptions);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    // ── DTOs internos (forma del payload de Keycloak) ──────────────────────────

    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")]   int    ExpiresIn);

    private record KeycloakUserJson(
        Guid Id,
        string? Email,
        string? FirstName,
        string? LastName,
        bool? Enabled);

    private record RealmRoleJson(Guid Id, string Name);
}
