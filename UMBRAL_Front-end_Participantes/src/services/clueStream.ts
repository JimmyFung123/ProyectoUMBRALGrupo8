import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClueStreamStatus, ReleasedClue } from '../types';
import { getReleasedClues } from './sessionService';

// Built-in exponential backoff schedule for SignalR's withAutomaticReconnect.
// Total ~62s of progressive retries before the hub gives up and we fall back
// to manual reconnection via the polling effect.
const RECONNECT_DELAYS_MS = [0, 2_000, 4_000, 8_000, 16_000, 30_000];

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

export interface UseClueStreamOptions {
  sessionId: string;
  teamId: string;
  /** Fires when a new clue arrives in real time (already filtered by team). */
  onClue?: (clue: ReleasedClue, isAutomatic: boolean) => void;
}

export interface UseClueStreamResult {
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
 * Subscribes to live clue notifications for a team via SignalR (HU-20).
 * On (re)connect, queries the API to ensure the in-memory list stays in sync
 * with what the back-end recorded, so missed events are recovered.
 */
export function useClueStream({
  sessionId,
  teamId,
  onClue,
}: UseClueStreamOptions): UseClueStreamResult {
  const [clues, setClues] = useState<ReleasedClue[]>([]);
  const [status, setStatus] = useState<ClueStreamStatus>('connecting');
  const [lastSyncAt, setLastSyncAt] = useState<Date | null>(null);

  // Refs for things that should not retrigger the effect
  const connectionRef = useRef<HubConnection | null>(null);
  const onClueRef = useRef(onClue);
  onClueRef.current = onClue;

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
