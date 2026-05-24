export interface SessionInfo {
  id: string;
  name: string;
  status: string;
  accessCode: string;
}

export interface TeamCreatedInfo {
  teamId: string;
  inviteCode: string;
}

export interface TeamJoinedInfo {
  teamId: string;
  teamName: string;
  inviteCode: string;
  memberCount: number;
}
