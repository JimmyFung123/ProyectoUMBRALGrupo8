import { useState } from 'react';
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

export default function App() {
  const [screen, setScreen] = useState<Screen>('join-session');
  const [session, setSession] = useState<SessionInfo | null>(null);
  const [nickname, setNickname] = useState('');
  const [team, setTeam] = useState<TeamState | null>(null);
  const [currentStage, setCurrentStage] = useState<ParticipantStage | null>(null);

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
        onBack={() => { setSession(null); setScreen('join-session'); }}
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
      />
    );
  }

  return <JoinSessionScreen onSessionFound={handleSessionFound} />;
}
