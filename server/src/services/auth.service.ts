import jwt from "jsonwebtoken";
import type { AuthUser } from "../types/user.js";

/** Access token expires in 1 hour. */
const ACCESS_TOKEN_EXPIRY = "1h";

/** Refresh token expires in 7 days. */
const REFRESH_TOKEN_EXPIRY = "7d";

/** Token type identifier for refresh tokens. */
const REFRESH_TOKEN_TYPE = "refresh";

/** Token type identifier for access tokens. */
const ACCESS_TOKEN_TYPE = "access";

interface RefreshTokenPayload extends AuthUser {
  type: typeof REFRESH_TOKEN_TYPE;
}

interface AccessTokenPayload extends AuthUser {
  type: typeof ACCESS_TOKEN_TYPE;
}

interface JwtTokenExpiredError extends Error {
  name: "TokenExpiredError";
}

export interface AuthTokenPair {
  accessToken: string;
  refreshToken: string;
}

const getJwtSecret = () => {
  const secret = process.env.JWT_SECRET ?? "";

  if (!secret) {
    throw new Error("JWT_SECRET is not configured");
  }

  return secret;
};

/**
 * Signs a JWT access token for the given user (1 hour expiry).
 *
 * @param user - Authenticated user to include in the token.
 * @returns A signed JWT access token string.
 */
export const signAccessToken = (user: AuthUser): string => {
  const secret = getJwtSecret();
  const payload: AccessTokenPayload = { ...user, type: ACCESS_TOKEN_TYPE };
  return jwt.sign(payload, secret, { expiresIn: ACCESS_TOKEN_EXPIRY });
};

/**
 * Signs a JWT refresh token for the given user (7 days expiry).
 *
 * @param user - Authenticated user to include in the token.
 * @returns A signed JWT refresh token string.
 */
export const signRefreshToken = (user: AuthUser): string => {
  const secret = getJwtSecret();
  const payload: RefreshTokenPayload = { ...user, type: REFRESH_TOKEN_TYPE };
  return jwt.sign(payload, secret, { expiresIn: REFRESH_TOKEN_EXPIRY });
};

/**
 * Signs both access and refresh tokens for the given user.
 *
 * @param user - Authenticated user to include in the tokens.
 * @returns An object containing both accessToken and refreshToken.
 */
export const signAuthTokenPair = (user: AuthUser): AuthTokenPair => ({
  accessToken: signAccessToken(user),
  refreshToken: signRefreshToken(user)
});

/**
 * Verifies an access token and returns the user payload.
 * Throws TokenExpiredError if expired.
 *
 * @param token - Bearer token without the prefix.
 * @returns The decoded user payload.
 * @throws TokenExpiredError if the token is expired.
 * @throws JsonWebTokenError if the token is invalid.
 */
export const verifyAccessToken = (token: string): AuthUser => {
  const secret = getJwtSecret();
  const decoded = jwt.verify(token, secret) as AccessTokenPayload;

  if (decoded.type && decoded.type !== ACCESS_TOKEN_TYPE) {
    throw new Error("Invalid token type: expected access token");
  }

  return { id: decoded.id, username: decoded.username };
};

/**
 * Verifies a refresh token and returns the user payload.
 *
 * @param token - Refresh token without the prefix.
 * @returns The decoded user payload.
 * @throws JsonWebTokenError if the token is invalid or expired.
 */
export const verifyRefreshToken = (token: string): AuthUser => {
  const secret = getJwtSecret();
  const decoded = jwt.verify(token, secret) as RefreshTokenPayload;

  if (decoded.type !== REFRESH_TOKEN_TYPE) {
    throw new Error("Invalid token type: expected refresh token");
  }

  return { id: decoded.id, username: decoded.username };
};

/**
 * Checks if an error is a TokenExpiredError from jwt.
 *
 * @param error - The error to check.
 * @returns True if the error is a TokenExpiredError.
 */
export const isTokenExpiredError = (error: unknown): error is JwtTokenExpiredError => {
  if (!(error instanceof Error)) {
    return false;
  }

  const tokenExpiredCtor = (jwt as unknown as { TokenExpiredError?: new (...args: unknown[]) => Error }).TokenExpiredError;

  if (typeof tokenExpiredCtor === "function" && error instanceof tokenExpiredCtor) {
    return true;
  }

  return error.name === "TokenExpiredError";
};
