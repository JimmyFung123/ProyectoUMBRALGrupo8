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

/** Usuario operador seed del realm `umbral` (scripts/keycloak/umbral-realm.json). */
export const OPERATOR_USER = process.env.OPERATOR_USER ?? 'admin@umbral.local';
export const OPERATOR_PASSWORD = process.env.OPERATOR_PASSWORD ?? 'Umbral2026!';

/** Sesión del operador (storageState) generada una vez en global-setup. */
export const OPERATOR_STORAGE = 'playwright/.auth/operator.json';
