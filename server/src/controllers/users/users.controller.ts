import type { Request, Response } from "express";
import type { DataSource } from "typeorm";
import UserEntity from "../../models/user.entity.js";
import type { UserProfile } from "../../types/user.js";

interface UsersController {
  getMe: (request: Request, response: Response) => Promise<void>;
  updateMe: (request: Request, response: Response) => Promise<void>;
}

/**
 * Maps a UserEntity to a safe profile response (no password hash).
 */
const toProfile = (entity: UserEntity): UserProfile => ({
  id: entity.id,
  username: entity.username,
  name: entity.name ?? null,
  age: entity.age ?? null,
  description: entity.description ?? null,
  rewardPoints: entity.rewardPoints,
  money: entity.money
});

/**
 * Builds the Users controller with injected data source.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns The Users controller handlers.
 */
export const createUsersController = (dataSource: DataSource): UsersController => {
  const userRepository = dataSource.getRepository(UserEntity);

  /**
   * Returns the authenticated user's profile.
   */
  const getMe: UsersController["getMe"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const user = await userRepository.findOne({ where: { id: request.user.id } });

      if (!user) {
        response.status(404).json({ message: "User not found" });
        return;
      }

      response.json({ user: toProfile(user) });
    } catch (error) {
      console.error("Failed to load user profile.", error);
      response.status(500).json({
        message: "Failed to load profile",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Updates the authenticated user's profile fields.
   */
  const updateMe: UsersController["updateMe"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const body = request.body as Record<string, unknown>;

    try {
      const user = await userRepository.findOne({ where: { id: request.user.id } });

      if (!user) {
        response.status(404).json({ message: "User not found" });
        return;
      }

      if (typeof body.name === "string") {
        user.name = body.name.trim() || null;
      }

      if (body.age !== undefined) {
        const age = typeof body.age === "number" ? body.age : null;
        user.age = age !== null && Number.isInteger(age) && age >= 0 && age <= 150 ? age : null;
      }

      if (typeof body.description === "string") {
        user.description = body.description.trim() || null;
      }

      if (typeof body.rewardPoints === "number" && Number.isInteger(body.rewardPoints)) {
        user.rewardPoints = Math.max(0, body.rewardPoints);
      }

      if (typeof body.money === "number" && Number.isInteger(body.money)) {
        user.money = Math.max(0, body.money);
      }

      const saved = await userRepository.save(user);
      response.json({ user: toProfile(saved) });
    } catch (error) {
      console.error("Failed to update user profile.", error);
      response.status(500).json({
        message: "Failed to update profile",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return { getMe, updateMe };
};
