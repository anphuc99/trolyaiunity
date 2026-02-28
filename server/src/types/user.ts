export interface AuthUser {
  id: number;
  username: string;
}

export interface UserProfile extends AuthUser {
  name?: string | null;
  age?: number | null;
  description?: string | null;
  voiceName?: string | null;
  pitch?: number | null;
  levelId?: number | null;
  level?: string | null;
  levelDescription?: string | null;
  currentStoryId?: number | null;
}
