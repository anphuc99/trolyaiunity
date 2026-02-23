import type { Request, Response } from "express";
import {
  signAccessToken,
  signRefreshToken,
  verifyRefreshToken,
  isTokenExpiredError
} from "../../services/auth.service.js";

interface TokenValidatePayload {
  refreshToken: string;
}

interface TokenRefreshPayload {
  refreshToken: string;
}

interface TokenController {
  /**
   * Validates a refresh token and extends its duration (7 days).
   * Returns a new refresh token.
   */
  validateToken: (request: Request, response: Response) => void;

  /**
   * Uses a refresh token to get a new access token.
   * Does not renew the refresh token.
   */
  refreshToken: (request: Request, response: Response) => void;
}

/**
 * Builds the token controller for authentication token operations.
 *
 * @returns The token controller handlers.
 */
export const createTokenController = (): TokenController => {
  /**
   * POST /token/validate
   * Validates a refresh token and returns a new refresh token (extends 7 days).
   *
   * Body: { refreshToken: string }
   * Response: { valid: true, refreshToken: string } or { valid: false, ... }
   */
  const validateToken: TokenController["validateToken"] = (request, response) => {
    const payload = request.body as TokenValidatePayload;
    const refreshToken = typeof payload?.refreshToken === "string" ? payload.refreshToken.trim() : "";

    if (!refreshToken) {
      response.status(400).json({
        valid: false,
        message: "Refresh token is required"
      });
      return;
    }

    try {
      const user = verifyRefreshToken(refreshToken);

      // Issue a new refresh token (extends expiry by 7 days)
      const newRefreshToken = signRefreshToken(user);

      response.json({
        valid: true,
        refreshToken: newRefreshToken
      });
    } catch (error) {
      console.error("Token validation failed.", error);

      if (isTokenExpiredError(error)) {
        response.status(401).json({
          valid: false,
          code: "TOKEN_EXPIRED",
          message: "Refresh token has expired"
        });
        return;
      }

      response.status(401).json({
        valid: false,
        message: "Invalid refresh token",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * POST /token/refresh
   * Uses a valid refresh token to get a new access token.
   *
   * Body: { refreshToken: string }
   * Response: { accessToken: string, refreshToken?: string } or error
   */
  const refreshToken: TokenController["refreshToken"] = (request, response) => {
    const payload = request.body as TokenRefreshPayload;
    const token = typeof payload?.refreshToken === "string" ? payload.refreshToken.trim() : "";

    if (!token) {
      response.status(400).json({
        message: "Refresh token is required"
      });
      return;
    }

    try {
      const user = verifyRefreshToken(token);

      // Issue a new access token
      const newAccessToken = signAccessToken(user);

      response.json({
        accessToken: newAccessToken
      });
    } catch (error) {
      console.error("Token refresh failed.", error);

      if (isTokenExpiredError(error)) {
        response.status(401).json({
          code: "TOKEN_EXPIRED",
          message: "Refresh token has expired"
        });
        return;
      }

      response.status(401).json({
        message: "Invalid refresh token",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return {
    validateToken,
    refreshToken
  };
};
