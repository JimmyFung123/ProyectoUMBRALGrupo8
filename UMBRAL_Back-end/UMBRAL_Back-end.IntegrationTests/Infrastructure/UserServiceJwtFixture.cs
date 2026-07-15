namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Testcontainers.Keycloak;
using Xunit;

/// <summary>
/// <see cref="IAsyncLifetime"/> fixture for the real-JWT-validation suite that
/// exercises <c>UMBRAL.Auth.UmbralAuthExtensions.AddUmbralJwtAuth</c> end-to-end
/// (see <c>UMBRAL_Back-end/Shared/UMBRAL.Auth/UmbralAuthExtensions.cs</c>, lines
/// 37-124). That extension is registered for real by every service's
/// <c>Program.cs</c> (UserService's at line 44), but until this suite it had NEVER
/// been exercised against a token actually issued and signed by Keycloak — every
/// existing test either bypasses it entirely (<see cref="AdminTestAuthHandler"/>,
/// <see cref="TestAuthHandler"/>) or fabricates the flattened <c>umbral_role</c>
/// claims by hand. In particular, <c>OnTokenValidated</c>'s manual parsing of the
/// <c>realm_access</c> claim (a JSON string) into <c>Claim("umbral_role", ...)</c>
/// entries — with a silent <c>catch { }</c> if parsing fails — had zero real
/// coverage: a malformed/missing claim would leave a user authenticated but with NO
/// roles, and no test would ever notice.
///
/// <b>Deliberately separate from <see cref="UserServiceKeycloakFixture"/></b> (the
/// Admin API / camelCase-serialization regression suite). That fixture imports
/// <c>scripts/keycloak/umbral-realm.json</c> AS-IS — the same file
/// <c>docker-compose.yml</c> uses — specifically so it proves the real
/// "umbral-backend" wiring without drifting from prod. This fixture instead imports
/// <c>Infrastructure/TestResources/umbral-realm-with-ropc.json</c>, a TEST-ONLY COPY
/// of that same realm with two deliberate additions (documented here, not in the
/// JSON — JSON has no comments):
///   1. A new public client <c>"umbral-test-ropc"</c>
///      (<c>directAccessGrantsEnabled: true</c>, <c>standardFlowEnabled: false</c>,
///      no service account, no redirect URIs) — needed because NEITHER real client
///      in the actual realm supports the "password" grant: <c>umbral-frontend</c> is
///      PKCE-only and <c>umbral-backend</c> is a client-credentials service account,
///      not a user-token client. Without a ROPC-enabled client there is no simple
///      way to obtain a real user access token for a test.
///   2. A new user <c>operator-test@umbral.local</c> with realm role
///      <c>"operator"</c> only (the existing <c>admin@umbral.local</c> / role
///      "admin" is reused as-is for the admin case, not duplicated).
/// The real realm (<c>scripts/keycloak/umbral-realm.json</c>) is NEVER modified —
/// this divergence lives only in the test-resources copy above.
///
/// Unlike <see cref="UserServiceApiFactory"/>'s default construction (used by
/// <see cref="UserServiceKeycloakFixture"/>), the factory built here passes a
/// non-null <c>keycloakAuthority</c>, which tells
/// <see cref="UserServiceApiFactory.ConfigureWebHost"/> to leave the REAL
/// <c>AddUmbralJwtAuth</c> pipeline active instead of swapping it for
/// <see cref="AdminTestAuthHandler"/> — that bypass is exactly what this suite must
/// NOT have, since exercising the real validation path is the point.
/// </summary>
public class UserServiceJwtFixture : IAsyncLifetime
{
    private const string RealmName = "umbral";
    private const string AdminClientId = "umbral-backend";
    private const string AdminClientSecret = "umbral-backend-secret-change-me";

    /// <summary>Test-only ROPC client id — see class doc comment, point 1.</summary>
    public const string RopcClientId = "umbral-test-ropc";

    /// <summary>Reused from the real realm export — role "admin".</summary>
    public const string AdminUsername = "admin@umbral.local";
    public const string AdminPassword = "Umbral2026!";

    /// <summary>Test-only user — see class doc comment, point 2 — role "operator" only.</summary>
    public const string OperatorUsername = "operator-test@umbral.local";
    public const string OperatorPassword = "Operator2026!";

    private KeycloakContainer _container = null!;
    private HttpClient _tokenClient = null!;

    public UserServiceApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var realmFilePath = FindRealmFile();

        _container = new KeycloakBuilder("quay.io/keycloak/keycloak:25.0")
            .WithRealm(realmFilePath)
            .Build();

        await _container.StartAsync();

        var adminBaseUrl = _container.GetBaseAddress();
        var authority = $"{adminBaseUrl.TrimEnd('/')}/realms/{RealmName}";
        _tokenClient = new HttpClient { BaseAddress = new Uri(authority + "/") };

        Factory = new UserServiceApiFactory(
            adminBaseUrl, RealmName, AdminClientId, AdminClientSecret,
            keycloakAuthority: authority);
    }

    public async Task DisposeAsync()
    {
        // Null-guarded: if InitializeAsync failed partway (e.g. the container never
        // started), some fields below are still unassigned. xUnit calls DisposeAsync
        // regardless, so dereferencing them unconditionally would mask the real
        // failure behind a NullReferenceException from cleanup instead.
        _tokenClient?.Dispose();

        try
        {
            if (Factory is not null)
                await Factory.DisposeAsync();
        }
        finally
        {
            if (_container is not null)
                await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Requests a REAL access token from Keycloak via the Resource Owner Password
    /// Credentials grant (<c>grant_type=password</c>) against the test-only
    /// <see cref="RopcClientId"/> client. This is the only way to obtain a
    /// Keycloak-signed JWT for a specific user in this suite — see the class doc
    /// comment for why neither real client supports this grant.
    /// </summary>
    public async Task<string> GetAccessTokenAsync(string username, string password)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = RopcClientId,
            ["username"] = username,
            ["password"] = password,
        };

        using var response = await _tokenClient.PostAsync(
            "protocol/openid-connect/token", new FormUrlEncodedContent(form));

        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Keycloak token request for '{username}' failed with " +
                $"{(int)response.StatusCode} {response.StatusCode}: {body}");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("access_token", out var token) && token.GetString() is { } value
            ? value
            : throw new InvalidOperationException(
                $"Keycloak token response for '{username}' had no access_token: {body}");
    }

    /// <summary>
    /// Walks up from the test assembly's output directory (bin/Debug/net10.0/…)
    /// until it finds the repo root (marked by docker-compose.yml) — same technique
    /// as <see cref="UserServiceKeycloakFixture.FindRealmFile"/>, but pointing at
    /// the test-only realm copy under this project's own TestResources folder
    /// instead of scripts/keycloak/.
    /// </summary>
    private static string FindRealmFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docker-compose.yml")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException(
                "Could not locate repo root (docker-compose.yml) above " + AppContext.BaseDirectory);

        return Path.Combine(
            dir.FullName, "UMBRAL_Back-end", "UMBRAL_Back-end.IntegrationTests",
            "Infrastructure", "TestResources", "umbral-realm-with-ropc.json");
    }
}

[CollectionDefinition(Name)]
public class UserServiceJwtCollection : ICollectionFixture<UserServiceJwtFixture>
{
    public const string Name = "UserService real JWT validation tests";
}
