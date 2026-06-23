import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClueStreamStatus, ReleasedClue } from '../types';
import { getReleasedClues } from './sessionService';

/**
 * HU-20 + HU-28 — single SignalR connection for everything a participant's
 * GameScreen cares about: live clues, stage results, operator broadcasts and
 * penalties. Used to be two separate hooks (useClueStream + useGameEvents),
 * each opening its own WebSocket to the same hub and the same session group —
 * the server ended up sending every event twice to the same device. Both were
 * only ever called together from GameScreen with the same (sessionId, teamId),
 * so merging them removes the duplicate connection without losing anything.
 */

// Built-in exponential backoff schedule for SignalR's withAutomaticReconnect.
// Total ~62s of progressive retries before the hub gives up and we fall back
// to manual reconnection via the polling effect.
const RECONNECT_DELAYS_MS = [0, 2_000, 4_000, 8_000, 16_000, 30_000];

// Tighter timeouts than the SignalR default (30 s) so the "Reconectando…"
// badge fires within ~6 s of a network drop. The server-side AddSignalR()
// uses matching numbers (KeepAlive 3 s / ClientTimeout 6 s) so both sides
// agree on what "dead" means. The 3 s / 6 s ratio tolerates WiFi/4G jitter
// spikes without triggering false reconnects on weak networks.
const SERVER_TIMEOUT_MS = 6_000;
const KEEP_ALIVE_MS = 3_000;

// Hub URL — defaults to the relative path so the Vite proxy / tunnel works.
const HUB_URL = (import.meta.env.VITE_SESSION_HUB_URL as string | undefined) ?? '/hubs/session';

interface ClueReleasedPayload {
  sessionId: string;
  teamId: string;
  clueContent: string | null;
  clueLatitude: number | null;
  clueLongitude: number | null;
  clueRadiusMeters: number | null;
  clueNumber: number;
  isAutomatic?: boolean;
}

export interface StageCompletedPayload {
  sessionId: string;
  teamId: string;
  stageOrder: number;
  stageType: 'Trivia' | 'TreasureHunt';
  wasCorrect: boolean;
  pointsEarned: number;
  newScore: number;
  nextStageOrder: number;
  isLastStage: boolean;
}

export interface OperatorMessagePayload {
  sessionId: string;
  message: string;
  actorName: string;
  deliveredAt: string;
}

export interface TeamPenalizedPayload {
  sessionId: string;
  teamId: string;
  teamName: string;
  points: number;
  reason: string;
  newScore: number;
  actorName: string;
}

export interface UseGameConnectionOptions {
  sessionId: string;
  teamId: string;
  /** Fires when a new clue arrives in real time (already filtered by team). */
  onClue?: (clue: ReleasedClue, isAutomatic: boolean) => void;
  /** Called when the team just finished a stage (correct trivia OR scanned QR). */
  onStageCompleted?: (payload: StageCompletedPayload) => void;
  /** Called when the operator broadcasts a live message to all participants. */
  onOperatorMessage?: (payload: OperatorMessagePayload) => void;
  /** Called when the operator applied a penalty to THIS team (filtered by teamId). */
  onTeamPenalized?: (payload: TeamPenalizedPayload) => void;
}

export interface UseGameConnectionResult {
  /** Full ordered list of clues the team has received for the current stage. */
  clues: ReleasedClue[];
  /** Live connection state for the connection indicator. */
  status: ClueStreamStatus;
  /** Timestamp of the last successful sync (via SignalR push or HTTP fetch). */
  lastSyncAt: Date | null;
  /** Resets the local cache — useful when the team advances to a new stage. */
  resetClues: () => void;
}

/**
 * Opens a single SignalR connection scoped to one (sessionId, teamId) pair
 * and wires up every event a GameScreen needs. On (re)connect, queries the
 * API for released clues so the in-memory list stays in sync with what the
 * back-end recorded (missed events are recovered).
 */
export function useGameConnection({
  sessionId,
  teamId,
  onClue,
  onStageCompleted,
  onOperatorMessage,
  onTeamPenalized,
}: UseGameConnectionOptions): UseGameConnectionResult {
  const [clues, setClues] = useState<ReleasedClue[]>([]);
  const [status, setStatus] = useState<ClueStreamStatus>('connecting');
  const [lastSyncAt, setLastSyncAt] = useState<Date | null>(null);

  // Refs so callbacks can change without re-spinning the connection.
  const connectionRef = useRef<HubConnection | null>(null);
  const onClueRef = useRef(onClue);
  const onStageCompletedRef = useRef(onStageCompleted);
  const onOperatorMessageRef = useRef(onOperatorMessage);
  const onTeamPenalizedRef = useRef(onTeamPenalized);
  useEffect(() => {
    onClueRef.current = onClue;
    onStageCompletedRef.current = onStageCompleted;
    onOperatorMessageRef.current = onOperatorMessage;
    onTeamPenalizedRef.current = onTeamPenalized;
  });

  const syncFromApi = useCallback(async () => {
    try {
      const remote = await getReleasedClues(sessionId, teamId);
      setClues(remote.clues);
      setLastSyncAt(new Date());
    } catch {
      // ignore — connection indicator will reflect the issue
    }
  }, [sessionId, teamId]);

  const resetClues = useCallback(() => setClues([]), []);

  useEffect(() => {
    let cancelled = false;

    const connection = new HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect(RECONNECT_DELAYS_MS)
      .configureLogging(LogLevel.Warning)
      .build();

    // Apply the aggressive ping schedule. These are properties on the built
    // connection object — the builder API has no fluent equivalent in the
    // version of @microsoft/signalr we use.
    connection.serverTimeoutInMilliseconds = SERVER_TIMEOUT_MS;
    connection.keepAliveIntervalInMilliseconds = KEEP_ALIVE_MS;

    connectionRef.current = connection;

    connection.on('ClueReleased', (payload: ClueReleasedPayload) => {
      // The hub broadcasts to the whole session group — filter to our team only.
      if (payload.teamId !== teamId) return;

      const newClue: ReleasedClue = {
        // The hub does not send the clue's persistent ID; we use clueNumber to dedupe.
        id: `clue-${payload.clueNumber}`,
        order: payload.clueNumber,
        content: payload.clueContent,
        latitude: payload.clueLatitude,
        longitude: payload.clueLongitude,
        radiusMeters: payload.clueRadiusMeters,
      };

      setClues((prev) => {
        if (prev.some((c) => c.order === newClue.order)) return prev;
        return [...prev, newClue].sort((a, b) => a.order - b.order);
      });
      setLastSyncAt(new Date());
      onClueRef.current?.(newClue, payload.isAutomatic === true);
    });

    connection.on('StageCompleted', (payload: StageCompletedPayload) => {
      if (payload.teamId !== teamId) return;
      onStageCompletedRef.current?.(payload);
    });

    connection.on('OperatorMessage', (payload: OperatorMessagePayload) => {
      onOperatorMessageRef.current?.(payload);
    });

    connection.on('TeamPenalized', (payload: TeamPenalizedPayload) => {
      // Broadcast goes to the whole session group — only this team's
      // participants should see the toast, the rest just see the ranking
      // change through the regular SessionStateChanged refresh.
      if (payload.teamId !== teamId) return;
      onTeamPenalizedRef.current?.(payload);
    });

    connection.onreconnecting(() => {
      if (cancelled) return;
      setStatus('reconnecting');
    });

    connection.onreconnected(() => {
      if (cancelled) return;
      setStatus('connected');
      // Re-join the session group (group membership is per-connection) and resync.
      connection.invoke('JoinSession', sessionId).catch(() => { /* swallow */ });
      void syncFromApi();
    });

    connection.onclose(() => {
      if (cancelled) return;
      setStatus('disconnected');
    });

    async function start() {
      try {
        setStatus('connecting');
        await connection.start();
        if (cancelled) {
          await connection.stop();
          return;
        }
        await connection.invoke('JoinSession', sessionId);
        setStatus('connected');
        await syncFromApi();
      } catch {
        if (cancelled) return;
        setStatus('disconnected');
        // Fallback: even without the hub, fetch the current state so the UI
        // still shows the clues already released.
        void syncFromApi();
      }
    }

    void start();

    return () => {
      cancelled = true;
      if (connection.state !== HubConnectionState.Disconnected) {
        connection.stop().catch(() => { /* swallow */ });
      }
      connectionRef.current = null;
    };
  }, [sessionId, teamId, syncFromApi]);

  return { clues, status, lastSyncAt, resetClues };
}
