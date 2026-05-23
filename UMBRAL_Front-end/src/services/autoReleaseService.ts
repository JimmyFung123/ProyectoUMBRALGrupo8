import { stageService } from './stageService';

export const autoReleaseService = {
  configure(
    _missionId: string,
    stageId: string,
    payload: { timeMinutes: number | null; maxAttempts: number | null },
  ): Promise<void> {
    return stageService.setAutoRelease(stageId, payload);
  },
};
