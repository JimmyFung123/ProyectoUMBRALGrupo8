namespace UserService.Domain.Users;

using UserService.Domain.Common;

/// <summary>
/// Domain errors raised by HU-23 handlers. Messages are in Spanish because
/// they bubble up directly to the operator UI.
/// </summary>
public static class UserErrors
{
    // ── Validación de entrada ──────────────────────────────────────────────────

    public static readonly Error InvalidEmail =
        new("User.InvalidEmail", "El correo electrónico no es válido.");

    public static readonly Error InvalidName =
        new("User.InvalidName", "El nombre y el apellido son obligatorios.");

    public static readonly Error InvalidRole =
        new("User.InvalidRole", "Rol no soportado. Roles válidos: Admin u Operator.");

    // ── Reglas de negocio (HU-23) ──────────────────────────────────────────────

    /// <summary>HU-23, Flujo alterno: el correo ya está registrado.</summary>
    public static readonly Error EmailAlreadyInUse =
        new("User.EmailAlreadyInUse", "Este correo ya está en uso.");

    public static readonly Error NotFound =
        new("User.NotFound", "Usuario no encontrado.");

    /// <summary>HU-23, Criterio 4: un administrador no puede deshabilitar su
    /// propia cuenta — debe transferir el rol primero.</summary>
    public static readonly Error CannotDisableSelf =
        new("User.CannotDisableSelf", "No puedes deshabilitar tu propia cuenta.");

    /// <summary>HU-23, Criterio 4: si es el único administrador activo del
    /// sistema, no puede deshabilitarse ni degradarse a operador.</summary>
    public static readonly Error CannotDemoteLastAdmin =
        new("User.CannotDemoteLastAdmin",
            "No puedes reducir el rol del único administrador activo del sistema.");

    public static readonly Error CannotDisableLastAdmin =
        new("User.CannotDisableLastAdmin",
            "No puedes deshabilitar al único administrador activo del sistema.");

    // ── Errores de integración con Keycloak ────────────────────────────────────

    public static readonly Error KeycloakUnavailable =
        new("User.KeycloakUnavailable",
            "No se pudo contactar al proveedor de identidades. Intenta de nuevo.");

    public static readonly Error EmailDeliveryFailed =
        new("User.EmailDeliveryFailed",
            "La clave temporal fue restablecida, pero no se pudo enviar el correo. Revisá la configuración de SMTP.");
}
