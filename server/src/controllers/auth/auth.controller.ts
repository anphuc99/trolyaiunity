import type { Request, Response } from "express";
import bcrypt from "bcryptjs";
import type { DataSource } from "typeorm";
import UserEntity from "../../models/user.entity.js";
import {
  signAuthTokenPair,
  verifyRefreshToken,
  type AuthTokenPair
} from "../../services/auth.service.js";

interface AuthController {
  register: (request: Request, response: Response) => Promise<void>;
  login: (request: Request, response: Response) => Promise<void>;
  refreshToken: (request: Request, response: Response) => Promise<void>;
}

const BCRYPT_ROUNDS = 10;

/**
 * Builds the Auth controller with injected data source.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns The Auth controller handlers.
 */
export const createAuthController = (dataSource: DataSource): AuthController => {
  const userRepository = dataSource.getRepository(UserEntity);

  /**
   * Registers a new user account.
   * Optionally requires a REGISTRATION_TOKEN env var for gated signup.
   */
  const register: AuthController["register"] = async (request, response) => {
    const body = request.body as Record<string, unknown>;
    const username = typeof body.username === "string" ? body.username.trim() : "";
    const password = typeof body.password === "string" ? body.password : "";
    const name = typeof body.name === "string" ? body.name.trim() : null;
    const age = typeof body.age === "number" && Number.isInteger(body.age) ? body.age : null;
    const description = typeof body.description === "string" ? body.description.trim() : null;

    if (!username || !password) {
      response.status(400).json({ message: "Username and password are required" });
      return;
    }

    if (password.length < 4) {
      response.status(400).json({ message: "Password must be at least 4 characters" });
      return;
    }

    // Gated registration (optional)
    const registrationToken = process.env.REGISTRATION_TOKEN;
    if (registrationToken) {
      const providedToken = typeof body.registrationToken === "string" ? body.registrationToken : "";
      if (providedToken !== registrationToken) {
        response.status(403).json({ message: "Invalid registration token" });
        return;
      }
    }

    try {
      const existing = await userRepository.findOne({ where: { username } });
      if (existing) {
        response.status(409).json({ message: "Username already exists" });
        return;
      }

      const passwordHash = await bcrypt.hash(password, BCRYPT_ROUNDS);

      const user = userRepository.create({
        username,
        passwordHash,
        name: name || null,
        age,
        description: description || null,
        rewardPoints: 0,
        money: 0
      });

      const saved = await userRepository.save(user);
      const tokens = signAuthTokenPair({ id: saved.id, username: saved.username });

      response.status(201).json(tokens);
    } catch (error) {
      console.error("Registration failed.", error);
      response.status(500).json({
        message: "Registration failed",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Authenticates a user and returns a token pair.
   */
  const login: AuthController["login"] = async (request, response) => {
    const body = request.body as Record<string, unknown>;
    const username = typeof body.username === "string" ? body.username.trim() : "";
    const password = typeof body.password === "string" ? body.password : "";

    if (!username || !password) {
      response.status(400).json({ message: "Username and password are required" });
      return;
    }

    try {
      const user = await userRepository.findOne({ where: { username } });
      if (!user) {
        response.status(401).json({ message: "Invalid credentials" });
        return;
      }

      const isMatch = await bcrypt.compare(password, user.passwordHash);
      if (!isMatch) {
        response.status(401).json({ message: "Invalid credentials" });
        return;
      }

      const tokens = signAuthTokenPair({ id: user.id, username: user.username });
      response.json(tokens);
    } catch (error) {
      console.error("Login failed.", error);
      response.status(500).json({
        message: "Login failed",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Exchanges a refresh token for a new token pair.
   */
  const refreshToken: AuthController["refreshToken"] = async (request, response) => {
    const body = request.body as Record<string, unknown>;
    const token = typeof body.refreshToken === "string" ? body.refreshToken : "";

    if (!token) {
      response.status(400).json({ message: "Refresh token is required" });
      return;
    }

    try {
      const payload = verifyRefreshToken(token);

      // Verify user still exists
      const user = await userRepository.findOne({ where: { id: payload.id } });
      if (!user) {
        response.status(401).json({ message: "User no longer exists" });
        return;
      }

      const tokens = signAuthTokenPair({ id: user.id, username: user.username });
      response.json(tokens);
    } catch (error) {
      console.error("Token refresh failed.", error);
      response.status(401).json({
        message: "Invalid refresh token",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return { register, login, refreshToken };
};
