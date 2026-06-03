export interface Clue {
  id: string;
  order: number;
  content: string | null;
  latitude: number | null;
  longitude: number | null;
  radiusMeters: number | null;
  autoReleaseAfterMinutes?: number;
}

export interface AddCluePayload {
  order: number;
  content?: string;
  latitude?: number;
  longitude?: number;
  radiusMeters?: number;
  autoReleaseAfterMinutes?: number;
}

export interface UpdateCluePayload {
  order: number;
  content?: string;
  latitude?: number;
  longitude?: number;
  radiusMeters?: number;
  autoReleaseAfterMinutes?: number;
}
