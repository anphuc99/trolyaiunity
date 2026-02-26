import bcrypt from "bcryptjs";
import type { Request, Response } from "express";
import type { DataSource } from "typeorm";
import LevelEntity from "../../models/level.entity.js";
import StoryEntity from "../../models/story.entity.js";
import UserEntity from "../../models/user.entity.js";
import { signAuthTokenPair } from "../../services/auth.service.js";
import type { AuthUser, UserProfile } from "../../types/user.js";

interface UserPayload {
  username: string;
  password: string;
  registerToken?: string;
}

interface ResetPasswordPayload {
  username: string;
  newPassword: string;
  registerToken?: string;
}

interface UserResponse {
  user: UserProfile;
  accessToken: string;
  refreshToken: string;
}

interface UsersController {
  register: (request: Request, response: Response) => Promise<void>;
  login: (request: Request, response: Response) => Promise<void>;
  resetPassword: (request: Request, response: Response) => Promise<void>;
  getMe: (request: Request, response: Response) => Promise<void>;
  updateLevel: (request: Request, response: Response) => Promise<void>;
  setCurrentStory: (request: Request, response: Response) => Promise<void>;
}

const normalizeUsername = (value: unknown) => {
  if (typeof value !== "string") {
    return "";
  }

  return value.trim().toLowerCase();
};

const getRegistrationToken = () => (process.env.REGISTRATION_TOKEN ?? "").trim();

const isValidPassword = (password: string) => password.length >= 6;

/**
 * Maps a user entity into a user profile response.
 *
 * @param user - The user entity (with optional level relation).
 * @returns A user profile payload for API responses.
 */
const toUserProfile = (user: UserEntity): UserProfile => ({
  id: user.id,
  username: user.username,
  levelId: user.levelId ?? null,
  level: user.level?.level ?? null,
  levelDescription: user.level?.descript ?? null,
  currentStoryId: user.currentStoryId ?? null
});

/**
 * Creates an auth response with user profile and token pair.
 *
 * @param user - The user entity.
 * @returns A response object containing user profile, accessToken (1h), and refreshToken (7d).
 */
const toAuthResponse = (user: UserEntity): UserResponse => {
  const authUser: AuthUser = { id: user.id, username: user.username };
  const tokens = signAuthTokenPair(authUser);

  return {
    user: toUserProfile(user),
    accessToken: tokens.accessToken,
    refreshToken: tokens.refreshToken
  };
};

/**
 * Builds the Users controller with injected data source dependencies.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns The Users controller handlers.
 */
export const createUsersController = (dataSource: DataSource): UsersController => {
  const repository = dataSource.getRepository(UserEntity);
  const levelRepository = dataSource.getRepository(LevelEntity);
  const storyRepository = dataSource.getRepository(StoryEntity);

  const register: UsersController["register"] = async (request, response) => {
    const payload = request.body as UserPayload;
    const username = normalizeUsername(payload?.username);
    const password = typeof payload?.password === "string" ? payload.password : "";
    const providedToken = typeof payload?.registerToken === "string" ? payload.registerToken.trim() : "";

    if (!username || !isValidPassword(password)) {
      response.status(400).json({
        message: "Username and password are required"
      });
      return;
    }

    const registrationToken = getRegistrationToken();

    if (!registrationToken) {
      response.status(500).json({ message: "Registration token is not configured" });
      return;
    }

    if (providedToken !== registrationToken) {
      response.status(403).json({ message: "Invalid registration token" });
      return;
    }

    try {
      const existing = await repository.findOne({ where: { username } });

      if (existing) {
        response.status(409).json({ message: "Username is already taken" });
        return;
      }

      const passwordHash = await bcrypt.hash(password, 10);
      const user = repository.create({ username, passwordHash });
      const saved = await repository.save(user);
      const hydrated = await repository.findOne({ where: { id: saved.id }, relations: { level: true } });

      response.status(201).json(toAuthResponse(hydrated ?? saved));
    } catch (error) {
      console.error("Failed to register user.", error);
      response.status(500).json({
        message: "Failed to register user",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  const login: UsersController["login"] = async (request, response) => {
    const payload = request.body as UserPayload;
    const username = normalizeUsername(payload?.username);
    const password = typeof payload?.password === "string" ? payload.password : "";

    if (!username || !password) {
      response.status(400).json({ message: "Username and password are required" });
      return;
    }

    try {
      const user = await repository.findOne({ where: { username }, relations: { level: true } });

      if (!user) {
        response.status(401).json({ message: "Invalid credentials" });
        return;
      }

      const isValid = await bcrypt.compare(password, user.passwordHash);

      if (!isValid) {
        response.status(401).json({ message: "Invalid credentials" });
        return;
      }

      response.json(toAuthResponse(user));
    } catch (error) {
      console.error("Failed to login.", error);
      response.status(500).json({
        message: "Failed to login",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Resets a user's password using the registration token (no old password required).
   *
   * @param request - Express request with reset payload.
   * @param response - Express response for reset results.
   */
  const resetPassword: UsersController["resetPassword"] = async (request, response) => {
    const payload = request.body as ResetPasswordPayload;
    const username = normalizeUsername(payload?.username);
    const newPassword = typeof payload?.newPassword === "string" ? payload.newPassword : "";
    const providedToken = typeof payload?.registerToken === "string" ? payload.registerToken.trim() : "";

    if (!username || !isValidPassword(newPassword)) {
      response.status(400).json({ message: "Username and new password are required" });
      return;
    }

    const registrationToken = getRegistrationToken();

    if (!registrationToken) {
      response.status(500).json({ message: "Registration token is not configured" });
      return;
    }

    if (providedToken !== registrationToken) {
      response.status(403).json({ message: "Invalid registration token" });
      return;
    }

    try {
      const user = await repository.findOne({ where: { username } });

      if (!user) {
        response.status(404).json({ message: "User not found" });
        return;
      }

      user.passwordHash = await bcrypt.hash(newPassword, 10);
      await repository.save(user);

      response.json({ message: "Password updated" });
    } catch (error) {
      console.error("Failed to reset password.", error);
      response.status(500).json({
        message: "Failed to reset password",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  const getMe: UsersController["getMe"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const user = await repository.findOne({ where: { id: request.user.id }, relations: { level: true } });

      if (!user) {
        response.status(404).json({ message: "User not found" });
        return;
      }

      response.json({ user: toUserProfile(user) });
    } catch (error) {
      console.error("Failed to load user profile.", error);
      response.status(500).json({
        message: "Failed to load user profile",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Updates the authenticated user's proficiency level.
   *
   * @param request - Express request with level payload.
   * @param response - Express response for update results.
   */
  const updateLevel: UsersController["updateLevel"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const payload = request.body as { levelId?: number };
    const levelId = Number(payload?.levelId);

    if (!Number.isInteger(levelId)) {
      response.status(400).json({ message: "Level is required" });
      return;
    }

    try {
      const level = await levelRepository.findOne({ where: { id: levelId } });

      if (!level) {
        response.status(404).json({ message: "Level not found" });
        return;
      }

      const user = await repository.findOne({ where: { id: request.user.id } });

      if (!user) {
        response.status(404).json({ message: "User not found" });
        return;
      }

      user.levelId = level.id;
      const saved = await repository.save(user);
      const hydrated = await repository.findOne({ where: { id: saved.id }, relations: { level: true } });

      response.json(toAuthResponse(hydrated ?? saved));
    } catch (error) {
      console.error("Failed to update user level.", error);
      response.status(500).json({
        message: "Failed to update user level",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Sets the authenticated user's current story.
   * Pass null to clear the current story.
   *
   * @param request - Express request with story payload.
   * @param response - Express response for update results.
   */
  const setCurrentStory: UsersController["setCurrentStory"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const payload = request.body as { storyId?: number | null };
    const storyId = payload?.storyId === null ? null : Number(payload?.storyId);

    // Allow null to clear the current story
    if (payload?.storyId !== null && !Number.isInteger(storyId)) {
      response.status(400).json({ message: "Story id is required" });
      return;
    }

    try {
      // If storyId is provided and not null, verify ownership
      if (storyId !== null) {
        const story = await storyRepository.findOne({
          where: { id: storyId, userId: request.user.id }
        });

        if (!story) {
          response.status(404).json({ message: "Story not found" });
          return;
        }
      }

      const user = await repository.findOne({ where: { id: request.user.id } });

      if (!user) {
        response.status(404).json({ message: "User not found" });
        return;
      }

      user.currentStoryId = storyId;
      const saved = await repository.save(user);
      const hydrated = await repository.findOne({ where: { id: saved.id }, relations: { level: true } });

      response.json({ user: toUserProfile(hydrated ?? saved) });
    } catch (error) {
      console.error("Failed to set current story.", error);
      response.status(500).json({
        message: "Failed to set current story",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return {
    register,
    login,
    resetPassword,
    getMe,
    updateLevel,
    setCurrentStory
  };
};
