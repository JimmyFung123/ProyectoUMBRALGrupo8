import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { useEffect, useRef } from 'react';

/**
 * HU-28 — SignalR listener for the participant-facing events that drive the
 * immersive feedback layer (toasts, vibration, transitions).
 *
 * Lives parallel to <c>useClueStream</c> so we can subscribe / unsubscribe
 * with a different lifecycle and so the clue-stream logic stays focused on
 * the visible clue list. Both connections target the same hub group, but
 * SignalR's connection pooling is fine — each adds one WebSocket.
 */

const RECONNECT_DELAYS_MS = [0, 2_000, 4_000, 8_000, 16_000, 30_000];
const SERVER_TIMEOUT_MS = 6_000;
const KEEP_ALIVE_MS = 3_000;

const HUB_URL = (import.meta.env.VITE_SESSION_HUB_URL as string | undefined) ?? '/hubs/session';

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

export interface UseGameEventsOptions {
  sessionId: string;
  teamId: string;
  /** Called when the team just finished a stage (correct trivia OR scanned QR). */
  onStageCompleted?: (payload: StageCompletedPayload) => void;
  /** Called when the operator broadcasts a live message to all participants. */
  onOperatorMessage?: (payload: OperatorMessagePayload) => void;
  /** Called when the operator applied a penalty to THIS team (filtered by teamId). */
  onTeamPenalized?: (payload: TeamPenalizedPayload) => void;
}

/**
 * Opens a dedicated SignalR connection scoped to one (sessionId, teamId)
 * pair. The supplied callbacks fire only when the event is relevant to this
 * team (stage-completed) or to everyone in the session (operator message).
 *
 * Connection lifecycle mirrors `useClueStream`: aggressive ping schedule,
 * automatic reconnect, and a safe teardown on unmount.
 */
export function useGameEvents({
  sessionId,
  teamId,
  onStageCompleted,
  onOperatorMessage,
  onTeamPenalized,
}: UseGameEventsOptions) {
  // Refs so callbacks can change without re-spinning the connection.
  const stageCompletedRef = useRef(onStageCompleted);
  const operatorMessageRef = useRef(onOperatorMessage);
  const teamPenalizedRef = useRef(onTeamPenalized);
  stageCompletedRef.current = onStageCompleted;
  operatorMessageRef.current = onOperatorMessage;
  teamPenalizedRef.current = onTeamPenalized;

  useEffect(() => {
    let cancelled = false;

    const connection: HubConnection = new HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect(RECONNECT_DELAYS_MS)
      .configureLogging(LogLevel.Warning)
      .build();

    connection.serverTimeoutInMilliseconds = SERVER_TIMEOUT_MS;
    connection.keepAliveIntervalInMilliseconds = KEEP_ALIVE_MS;

    connection.on('StageCompleted', (payload: StageCompletedPayload) => {
      // Hub broadcasts to the session group — filter to this team only.
      if (payload.teamId !== teamId) return;
      stageCompletedRef.current?.(payload);
    });

    connection.on('OperatorMessage', (payload: OperatorMessagePayload) => {
      operatorMessageRef.current?.(payload);
    });

    connection.on('TeamPenalized', (payload: TeamPenalizedPayload) => {
      // Broadcast goes to the whole session group — only this team's
      // participants should see the toast, the rest just see the ranking
      // change through the regular SessionStateChanged refresh.
      if (payload.teamId !== teamId) return;
      teamPenalizedRef.current?.(payload);
    });

    connection.onreconnected(() => {
      if (cancelled) return;
      connection.invoke('JoinSession', sessionId).catch(() => { /* swallow */ });
    });

    async function start() {
      try {
        await connection.start();
        if (cancelled) {
          await connection.stop();
          return;
        }
        await connection.invoke('JoinSession', sessionId);
      } catch {
        // Silent — useClueStream surfaces the connection state to the user.
      }
    }
    void start();

    return () => {
      cancelled = true;
      if (connection.state !== HubConnectionState.Disconnected) {
        connection.stop().catch(() => { /* swallow */ });
      }
    };
  }, [sessionId, teamId]);
}
