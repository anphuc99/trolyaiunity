export interface AuthUser {
  id: number;
  username: string;
}

export interface UserProfile extends AuthUser {
  levelId?: number | null;
  level?: string | null;
  levelDescription?: string | null;
}
