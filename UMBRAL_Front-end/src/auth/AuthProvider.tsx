import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import keycloak from './keycloak';

/**
 * keycloak-js no soporta ser inicializado dos veces. React Strict Mode (que
 * ejecuta useEffect dos veces en dev) rompe esa garantía. Usamos un flag a
 * nivel de módulo (no a nivel de componente) que sobrevive al re-mount del
 * Strict Mode y solo permite UN init() global en toda la vida de la pestaña.
 */
let keycloakInitStarted = false;

export interface UmbralUser {
  /** Stable Keycloak subject (sub claim). */
  id: string;
  email: string;
  /** Display name pulled from the `name` claim, falls back to email. */
  name: string;
  roles: string[];
}

export interface AuthContextValue {
  /** True after keycloak.init() finished (success or failure). The first
   *  render is gated by AuthProvider so consumers never see `ready=false`. */
  ready: boolean;
  /** True when the user has a valid access token. */
  isAuthenticated: boolean;
  user: UmbralUser | null;
  isAdmin: boolean;
  isOperator: boolean;
  /** Triggers the Keycloak login redirect. */
  login: () => void;
  /** Logs out from Keycloak and redirects back to the app root. */
  logout: () => void;
  /** Returns a valid access token, refreshing it if it expires within 30s.
   *  Used by the HTTP wrapper to attach `Authorization: Bearer …`. */
  getToken: () => Promise<string | null>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

function extractUser(): UmbralUser | null {
  const parsed = keycloak.tokenParsed as Record<string, unknown> | undefined;
  if (!parsed) return null;

  const sub = (parsed.sub as string | undefined) ?? '';
  const email = (parsed.email as string | undefined)
             ?? (parsed.preferred_username as string | undefined)
             ?? '';
  const name = (parsed.name as string | undefined)
            ?? (parsed.preferred_username as string | undefined)
            ?? email;
  const realmAccess = parsed.realm_access as { roles?: string[] } | undefined;
  const roles = Array.isArray(realmAccess?.roles) ? realmAccess!.roles : [];

  if (!sub) return null;
  return { id: sub, email, name, roles };
}

interface Props {
  children: ReactNode;
  /** Splash mostrado mientras keycloak-js termina de inicializar. */
  loadingFallback?: ReactNode;
}

/**
 * HU-23 frontend: envuelve la app y bloquea el render hasta que Keycloak
 * decida si el usuario tiene una sesión válida. Si no la tiene, dispara
 * el redirect a la pantalla de login del realm.
 *
 * Configuración elegida:
 * - onLoad='login-required'  → no hay vista pública en el operador, siempre
 *   se exige login antes de ver cualquier pantalla.
 * - pkceMethod='S256'        → flujo seguro para SPAs.
 * - checkLoginIframe=false   → evita el iframe-hidden que Chrome bloquea
 *   por cookies de tercero en dev; usamos refresh manual cada minuto.
 */
export function AuthProvider({ children, loadingFallback }: Props) {
  const [ready, setReady] = useState(false);
  const [, setTick] = useState(0); // forces re-render on token refresh
  const forceRerender = useCallback(() => setTick((t) => t + 1), []);

  useEffect(() => {
    let mounted = true;

    // Wire up callbacks BEFORE init so we never miss an event.
    keycloak.onAuthSuccess        = () => mounted && forceRerender();
    keycloak.onAuthRefreshSuccess = () => mounted && forceRerender();
    keycloak.onAuthLogout         = () => mounted && forceRerender();
    keycloak.onTokenExpired       = () => { void keycloak.updateToken(30).catch(() => keycloak.login()); };

    if (!keycloakInitStarted) {
      keycloakInitStarted = true;
      keycloak.init({
        onLoad: 'login-required',
        pkceMethod: 'S256',
        checkLoginIframe: false,
      })
        .catch(() => { /* swallow: the user will retry by reloading */ })
        .finally(() => { if (mounted) setReady(true); });
    } else if (keycloak.authenticated !== undefined) {
      // Strict Mode segundo mount: keycloak ya está listo, solo marcamos ready.
      setReady(true);
    }

    // Background refresh: try to renew if the token expires within 60s.
    const interval = setInterval(() => {
      void keycloak.updateToken(60).catch(() => { /* swallow; onTokenExpired handles it */ });
    }, 30_000);

    return () => {
      mounted = false;
      clearInterval(interval);
    };
  }, [forceRerender]);

  const value = useMemo<AuthContextValue>(() => {
    const user = extractUser();
    return {
      ready,
      isAuthenticated: keycloak.authenticated === true,
      user,
      isAdmin:    user?.roles.includes('admin')    ?? false,
      isOperator: user?.roles.includes('operator') ?? false,
      login: () => { void keycloak.login(); },
      logout: () => { void keycloak.logout({ redirectUri: window.location.origin }); },
      getToken: async () => {
        if (!keycloak.authenticated) return null;
        try {
          // Refresh if the token has less than 30s of life left.
          await keycloak.updateToken(30);
        } catch {
          // updateToken throws when the refresh token is also dead — fall
          // back to a full login.
          keycloak.login();
          return null;
        }
        return keycloak.token ?? null;
      },
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ready, keycloak.authenticated, keycloak.token]);

  if (!ready) {
    return <>{loadingFallback ?? <DefaultSplash />}</>;
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>');
  return ctx;
}

function DefaultSplash() {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      minHeight: '100vh', fontFamily: 'sans-serif', color: '#666',
    }}>
      <div style={{ textAlign: 'center' }}>
        <div style={{ fontSize: '2rem', marginBottom: '0.5rem' }}>🔐</div>
        <div>Verificando sesión…</div>
      </div>
    </div>
  );
}
