import { useEffect, useState } from 'react';
import { JoinSessionScreen } from './screens/JoinSessionScreen';
import { TeamChoiceScreen } from './screens/TeamChoiceScreen';
import { CreateTeamScreen } from './screens/CreateTeamScreen';
import { JoinTeamScreen } from './screens/JoinTeamScreen';
import { WaitingRoomScreen } from './screens/WaitingRoomScreen';
import { GameScreen } from './screens/GameScreen';
import type { ParticipantStage, SessionInfo, TeamCreatedInfo, TeamJoinedInfo } from './types';

type Screen = 'join-session' | 'team-choice' | 'create-team' | 'join-team' | 'waiting' | 'game';

type TeamState =
  | { isLeader: true; data: TeamCreatedInfo }
  | { isLeader: false; data: TeamJoinedInfo };

// Survives accidental reloads (Vite HMR reconnects, mobile background-kill, F5).
// sessionStorage is per-tab and clears on tab close — matches the spec's
// "identidad temporal de los participantes".
const STORAGE_KEY = 'umbral.participant.state.v1';

interface PersistedState {
  screen: Screen;
  session: SessionInfo | null;
  nickname: string;
  team: TeamState | null;
  currentStage: ParticipantStage | null;
}

function loadState(): PersistedState | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as PersistedState) : null;
  } catch {
    return null;
  }
}

function persistState(s: PersistedState) {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(s));
  } catch {
    // storage quota / private mode — ignore
  }
}

function clearPersistedState() {
  try { sessionStorage.removeItem(STORAGE_KEY); } catch { /* ignore */ }
}

export default function App() {
  const initial = loadState();

  const [screen, setScreen] = useState<Screen>(initial?.screen ?? 'join-session');
  const [session, setSession] = useState<SessionInfo | null>(initial?.session ?? null);
  const [nickname, setNickname] = useState(initial?.nickname ?? '');
  const [team, setTeam] = useState<TeamState | null>(initial?.team ?? null);
  const [currentStage, setCurrentStage] = useState<ParticipantStage | null>(initial?.currentStage ?? null);

  // Persist on every relevant change so reconnects/reloads don't drop the user out.
  useEffect(() => {
    if (screen === 'join-session' && !session && !team) {
      clearPersistedState();
      return;
    }
    persistState({ screen, session, nickname, team, currentStage });
  }, [screen, session, nickname, team, currentStage]);

  function handleSessionFound(s: SessionInfo) {
    setSession(s);
    setScreen('team-choice');
  }

  function handleTeamCreated(t: TeamCreatedInfo) {
    setTeam({ isLeader: true, data: t });
    setScreen('waiting');
  }

  function handleTeamJoined(t: TeamJoinedInfo) {
    setTeam({ isLeader: false, data: t });
    setScreen('waiting');
  }

  function handleGameStart(stage: ParticipantStage) {
    setCurrentStage(stage);
    setScreen('game');
  }

  function handleLeaveSession() {
    clearPersistedState();
    setSession(null);
    setNickname('');
    setTeam(null);
    setCurrentStage(null);
    setScreen('join-session');
  }

  if (screen === 'join-session') {
    return <JoinSessionScreen onSessionFound={handleSessionFound} />;
  }

  if (screen === 'team-choice' && session) {
    return (
      <TeamChoiceScreen
        session={session}
        nickname={nickname}
        onNicknameChange={setNickname}
        onCreateTeam={() => setScreen('create-team')}
        onJoinTeam={() => setScreen('join-team')}
        onBack={handleLeaveSession}
      />
    );
  }

  if (screen === 'create-team' && session) {
    return (
      <CreateTeamScreen
        session={session}
        nickname={nickname}
        onTeamCreated={handleTeamCreated}
        onBack={() => setScreen('team-choice')}
      />
    );
  }

  if (screen === 'join-team' && session) {
    return (
      <JoinTeamScreen
        session={session}
        nickname={nickname}
        onTeamJoined={handleTeamJoined}
        onBack={() => setScreen('team-choice')}
      />
    );
  }

  if (screen === 'waiting' && session && team) {
    const teamProp = team.isLeader
      ? { ...team.data, isLeader: true as const }
      : { ...team.data, isLeader: false as const };
    return (
      <WaitingRoomScreen
        session={session}
        team={teamProp}
        nickname={nickname}
        onGameStart={handleGameStart}
        onLeaveSession={handleLeaveSession}
      />
    );
  }

  if (screen === 'game' && session && team && currentStage) {
    const teamProp = { teamId: team.data.teamId, isLeader: team.isLeader };
    return (
      <GameScreen
        session={session}
        team={teamProp}
        nickname={nickname}
        initialStage={currentStage}
        onLeaveSession={handleLeaveSession}
      />
    );
  }

  // Fallback if persisted state is inconsistent (e.g., partial data after manual edits).
  return <JoinSessionScreen onSessionFound={handleSessionFound} />;
}
