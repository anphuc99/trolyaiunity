/**
 * Authenticated user payload extracted from JWT.
 */
export interface AuthUser {
  id: number;
  username: string;
}

/**
 * User profile returned by the /users/me endpoint.
 */
export interface UserProfile extends AuthUser {
  name?: string | null;
  age?: number | null;
  description?: string | null;
  rewardPoints: number;
  money: number;
}
