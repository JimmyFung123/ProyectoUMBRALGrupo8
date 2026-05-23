export interface Clue {
  id: string;
  order: number;
  content: string | null;
  latitude: number | null;
  longitude: number | null;
  radiusMeters: number | null;
}

export interface AddCluePayload {
  order: number;
  content?: string;
  latitude?: number;
  longitude?: number;
  radiusMeters?: number;
}

export interface UpdateCluePayload {
  order: number;
  content?: string;
  latitude?: number;
  longitude?: number;
  radiusMeters?: number;
}
