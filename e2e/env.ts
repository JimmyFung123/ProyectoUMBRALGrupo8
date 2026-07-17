/**
 * Configuración central del stack contra el que corren las E2E.
 * Todo es sobrescribible por variable de entorno (ver .env.example), así los
 * mismos tests corren contra Docker (default), dev-mode o CI sin tocar código.
 *
 * Los defaults apuntan al stack Docker local (docker-compose.deploy.yml), cuyas
 * imágenes de front vienen horneadas con las URLs localhost:* — todas
 * alcanzables desde el navegador que maneja Playwright en el host.
 */
export const OPERATOR_URL = process.env.OPERATOR_URL ?? 'http://localhost:5173';
export const PARTICIPANT_URL = process.env.PARTICIPANT_URL ?? 'http://localhost:5174';
export const KEYCLOAK_URL = process.env.KEYCLOAK_URL ?? 'http://localhost:18090';

/**
 * El front operador separa la UI por rol (ver UMBRAL_Front-end/src/App.jsx):
 *   - rol `admin`    → pestañas Misiones · Estadísticas · Sincronización · Personal.
 *   - rol `operator` → pestaña Sesiones (única).
 * La separación es EXCLUYENTE: un usuario admin NO ve Sesiones y viceversa. Por
 * eso el flujo completo necesita DOS identidades: el administrador crea la
 * misión y el operador gestiona la sesión en vivo.
 */

/** Administrador seed del realm `umbral` (scripts/keycloak/umbral-realm.json). */
export const ADMIN_USER = process.env.ADMIN_USER ?? 'admin@umbral.local';
export const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD ?? 'Umbral2026!';
/** Sesión del admin (storageState) generada una vez en global-setup. */
export const ADMIN_STORAGE = 'playwright/.auth/admin.json';

/**
 * Operador dedicado a E2E. El realm seed no trae ningún usuario con rol
 * `operator`, así que global-setup lo crea (idempotente) vía la Admin REST API
 * de Keycloak con contraseña permanente y sin acciones pendientes.
 */
export const OPERATOR_USER = process.env.OPERATOR_USER ?? 'operador.e2e@umbral.local';
export const OPERATOR_PASSWORD = process.env.OPERATOR_PASSWORD ?? 'Umbral2026!';
/** Sesión del operador (storageState) generada una vez en global-setup. */
export const OPERATOR_STORAGE = 'playwright/.auth/operator.json';

/**
 * Segundo operador de E2E, dedicado a RB-10 (una sesión solo la administra
 * quien la creó). El operador de arriba no alcanza porque el test necesita
 * DOS identidades reales y distintas cruzándose.
 */
export const OPERATOR_B_USER = process.env.OPERATOR_B_USER ?? 'operador-b.e2e@umbral.local';
export const OPERATOR_B_PASSWORD = process.env.OPERATOR_B_PASSWORD ?? 'Umbral2026!';
export const OPERATOR_B_STORAGE = 'playwright/.auth/operator-b.json';

/** Credenciales del admin de Keycloak (realm master) — solo para aprovisionar
 *  el usuario operador. Coinciden con docker-compose.yml (KEYCLOAK_ADMIN). */
export const KC_ADMIN_USER = process.env.KC_ADMIN_USER ?? 'admin';
export const KC_ADMIN_PASSWORD = process.env.KC_ADMIN_PASSWORD ?? 'admin';
export const KC_REALM = process.env.KC_REALM ?? 'umbral';

/**
 * Bases de las APIs de dominio (mismos puertos localhost:* que hornea el front
 * operador). Se usan para SEMBRAR la misión de prueba (misión + etapas + pistas
 * + regla "Intentos fallidos" + activación) por API en vez de manejar la densa
 * UI de administración: los controllers de Mission/Stage/Clue son públicos, y
 * lo que valida el test estrella (pistas por reintento y ranking "Finalizó") se
 * ejercita después SÍ por la UI real (operador + participantes + SignalR).
 */
export const MISSION_API = process.env.MISSION_API ?? 'http://localhost:5091/api';
export const STAGE_API = process.env.STAGE_API ?? 'http://localhost:5093/api';
export const CLUE_API = process.env.CLUE_API ?? 'http://localhost:5094/api';
