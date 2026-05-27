import * as signalR from '@microsoft/signalr';

const SIGNALR_URL =
  import.meta.env.VITE_SESSION_SIGNALR_URL ?? 'http://localhost:5092/hubs/session';

// Match the SessionService AddSignalR() config (KeepAlive 3 s / ClientTimeout 6 s)
// so disconnects surface in the UI within ~6 s, instead of waiting the 30 s
// SignalR default. The 3 s / 6 s ratio is the smallest SignalR-safe pair
// (timeout >= 2 * keep-alive) that still tolerates WiFi/4G jitter spikes.
const SERVER_TIMEOUT_MS = 6_000;
const KEEP_ALIVE_MS = 3_000;

/**
 * Hub events the operator dashboard cares about. Any of them being received
 * counts as "something changed, please refresh".
 *
 * SessionStateChanged → emitted by Start / Pause / Resume / Finalize /
 *   Penalize / ForceAdvance / submit-trivia / validate-qr handlers.
 * ClueReleased → emitted by ReleaseClue handler AND by the ClueAutoReleaseService
 *   background worker. The operator dashboard wants to know about both so the
 *   audit log refreshes when a clue is auto-released by the system.
 */
const REFRESH_EVENTS = ['SessionStateChanged', 'ClueReleased'] as const;

export type HubConnState = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

interface ConnectOptions {
  sessionId: string;
  /** Invoked whenever any refresh-worthy event is received. */
  onRefresh: () => void;
  /** Optional: receive connection-state transitions (used by the ranking panel
   *  to render "En vivo / Sincronizando / Desconectado"). */
  onStateChange?: (state: HubConnState) => void;
}

interface SessionHubHandle {
  /** Live SignalR connection (exposed for tests / debugging). */
  connection: signalR.HubConnection;
  /** Tears the connection down safely — waits for `start()` to settle so SignalR
   *  does not log "stopped during negotiation" under React Strict Mode. */
  dispose: () => void;
}

/**
 * Connects to /hubs/session, joins the session group, and wires `onRefresh`
 * to the events that matter for the operator dashboard.
 *
 * Why a shared helper?
 * 1) Under React Strict Mode the mount→unmount→re-mount cycle was tearing
 *    the connection down before `start()` finished, polluting the console
 *    with "stopped during negotiation". This helper sequences stop after
 *    start so the negotiation completes cleanly.
 * 2) ClueReleased is broadcast by the back-end auto-release worker, but the
 *    three operator panels were not listening for it — SignalR was logging
 *    "No client method with the name 'cluereleased' found" on every release.
 *    Listening here (instead of in three places) removes the warning AND
 *    makes the dashboard react to auto-released clues without waiting for
 *    the 10s polling tick.
 */
export function connectToSessionHub({ sessionId, onRefresh, onStateChange }: ConnectOptions): SessionHubHandle {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(SIGNALR_URL)
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  // Aggressive ping schedule — see SERVER_TIMEOUT_MS comment above.
  connection.serverTimeoutInMilliseconds = SERVER_TIMEOUT_MS;
  connection.keepAliveIntervalInMilliseconds = KEEP_ALIVE_MS;

  for (const event of REFRESH_EVENTS) {
    connection.on(event, () => onRefresh());
  }

  onStateChange?.('connecting');
  connection.onreconnecting(() => onStateChange?.('reconnecting'));
  connection.onreconnected(() => {
    onStateChange?.('connected');
    connection.invoke('JoinSession', sessionId).catch(() => { /* swallow */ });
    onRefresh();
  });
  connection.onclose(() => onStateChange?.('disconnected'));

  let cancelled = false;
  const startPromise = (async () => {
    try {
      await connection.start();
      if (cancelled) return;
      await connection.invoke('JoinSession', sessionId);
      onStateChange?.('connected');
    } catch {
      // Hub unreachable — polling fallback will keep the UI fresh.
      if (!cancelled) onStateChange?.('disconnected');
    }
  })();

  function dispose() {
    cancelled = true;
    // Wait for start() to resolve (or fail) BEFORE calling stop(), otherwise
    // SignalR aborts the in-flight negotiation and logs an ugly error.
    startPromise.finally(() => {
      if (connection.state !== signalR.HubConnectionState.Disconnected) {
        connection.invoke('LeaveSession', sessionId).catch(() => { /* swallow */ });
        connection.stop().catch(() => { /* swallow */ });
      }
    });
  }

  return { connection, dispose };
}
