import type { Request, Response, NextFunction } from "express";
import { verifyAccessToken, isTokenExpiredError } from "../services/auth.service.js";

/**
 * Ensures an authenticated user exists on the request.
 * Returns TOKEN_EXPIRED code when access token is expired (client should use refresh token).
 *
 * @param request - Express request.
 * @param response - Express response.
 * @param next - Express next middleware.
 */
export const requireAuth = (request: Request, response: Response, next: NextFunction) => {
  const header = request.headers.authorization ?? "";
  const [scheme, token] = header.split(" ");

  if (scheme !== "Bearer" || !token) {
    response.status(401).json({ message: "Unauthorized" });
    return;
  }

  try {
    request.user = verifyAccessToken(token);
    next();
  } catch (error) {
    console.error("Auth token verification failed.", error);

    if (isTokenExpiredError(error)) {
      response.status(401).json({
        code: "TOKEN_EXPIRED",
        message: "Access token has expired"
      });
      return;
    }

    response.status(401).json({
      message: "Invalid token",
      error: error instanceof Error ? error.message : "Unknown error"
    });
  }
};
