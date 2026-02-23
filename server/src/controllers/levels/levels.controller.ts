import type { Request, Response } from "express";
import type { DataSource } from "typeorm";
import LevelEntity from "../../models/level.entity.js";

interface LevelsController {
  getLevels: (request: Request, response: Response) => Promise<void>;
}

/**
 * Builds the Levels controller with injected data source dependencies.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns The Levels controller handlers.
 */
export const createLevelsController = (dataSource: DataSource): LevelsController => {
  const repository = dataSource.getRepository(LevelEntity);

  /**
   * Returns the list of configured proficiency levels.
   *
   * @param _request - Express request (unused).
   * @param response - Express response with level data.
   */
  const getLevels: LevelsController["getLevels"] = async (_request, response) => {
    try {
      const levels = await repository.find({ order: { id: "ASC" } });
      response.json({ levels });
    } catch (error) {
      console.error("Failed to load levels.", error);
      response.status(500).json({
        message: "Failed to load levels",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return {
    getLevels
  };
};
