namespace UMBRAL.Auth;

using System.Security.Claims;

/// <summary>
/// Extension methods to pull the operator's identity out of a
/// <see cref="ClaimsPrincipal"/> in a uniform way across services (HU-23).
///
/// Keycloak emits these standard claims for users:
///   - <c>preferred_username</c> → the email (also the username, since we
///     enabled <c>registrationEmailAsUsername</c> in the realm).
///   - <c>name</c> → "First Last".
///   - <c>email</c> → user's email.
///   - <c>realm_access.roles</c> → array; we flatten it into <c>umbral_role</c>
///     claims in <see cref="UmbralAuthExtensions"/>.
/// </summary>
public static class OperatorPrincipal
{
    public const string AdminRole    = "admin";
    public const string OperatorRole = "operator";

    /// <summary>
    /// Human-readable display name for audit logs. Picks the first available of:
    /// full name → username → email. Returns <c>null</c> when the principal is
    /// not authenticated (the audit handlers will then fall back to "Sistema"
    /// or to the legacy X-Operator-Name header).
    /// </summary>
    public static string? GetOperatorDisplayName(this ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true) return null;

        var name = user.FindFirstValue("name");
        if (!string.IsNullOrWhiteSpace(name)) return name.Trim();

        var preferred = user.FindFirstValue("preferred_username");
        if (!string.IsNullOrWhiteSpace(preferred)) return preferred.Trim();

        var email = user.FindFirstValue("email");
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    /// <summary>
    /// Stable Keycloak user id (the OIDC <c>sub</c> claim) for ownership checks
    /// (RB-10). Read literally as <c>"sub"</c> — <see cref="UmbralAuthExtensions"/>
    /// sets <c>MapInboundClaims = false</c>, so it is never remapped to
    /// <see cref="ClaimTypes.NameIdentifier"/>.
    /// </summary>
    public static string? GetOperatorId(this ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true) return null;

        var sub = user.FindFirstValue("sub");
        return string.IsNullOrWhiteSpace(sub) ? null : sub.Trim();
    }

    public static bool IsAdmin(this ClaimsPrincipal? user)
        => user?.IsInRole(AdminRole) == true;

    public static bool IsOperator(this ClaimsPrincipal? user)
        => user?.IsInRole(OperatorRole) == true;
}
