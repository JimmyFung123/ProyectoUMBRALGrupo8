import Keycloak from 'keycloak-js';

/**
 * HU-23: instancia única de keycloak-js compartida por toda la app.
 * Apunta al realm "umbral" definido en scripts/keycloak/umbral-realm.json
 * y al client público `umbral-frontend` con PKCE habilitado.
 *
 * El URL del servidor se puede sobrescribir vía variable de entorno
 * VITE_KEYCLOAK_URL para soportar deploys en otros hosts.
 */
const keycloak = new Keycloak({
  url:      import.meta.env.VITE_KEYCLOAK_URL      ?? 'http://localhost:8090',
  realm:    import.meta.env.VITE_KEYCLOAK_REALM    ?? 'umbral',
  clientId: import.meta.env.VITE_KEYCLOAK_CLIENT_ID ?? 'umbral-frontend',
});

export default keycloak;
