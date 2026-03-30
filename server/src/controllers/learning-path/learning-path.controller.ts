import type { Request, Response } from "express";
import type { DataSource } from "typeorm";
import LearningPathEntity from "../../models/learning-path.entity.js";

interface LearningPathPayload {
  level?: string;
  vocabulary?: string;
}

interface LearningPathResponse {
  id: number;
  level: string;
  vocabulary: string;
  createdAt: string;
  updatedAt: string;
}

interface LearningPathController {
  listLearningPaths: (request: Request, response: Response) => Promise<void>;
  getLearningPath: (request: Request, response: Response) => Promise<void>;
  createLearningPath: (request: Request, response: Response) => Promise<void>;
  updateLearningPath: (request: Request, response: Response) => Promise<void>;
  deleteLearningPath: (request: Request, response: Response) => Promise<void>;
}

const parseId = (value: string) => {
  const id = Number.parseInt(value, 10);
  return Number.isNaN(id) ? null : id;
};

const toResponse = (entity: LearningPathEntity): LearningPathResponse => ({
  id: entity.id,
  level: entity.level,
  vocabulary: entity.vocabulary,
  createdAt: entity.createdAt.toISOString(),
  updatedAt: entity.updatedAt.toISOString()
});

/**
 * Builds the LearningPath controller with injected data source dependencies.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns The LearningPath controller handlers.
 */
export const createLearningPathController = (dataSource: DataSource): LearningPathController => {
  const repository = dataSource.getRepository(LearningPathEntity);

  /**
   * Lists learning paths for the authenticated user.
   */
  const listLearningPaths: LearningPathController["listLearningPaths"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const items = await repository.find({
        where: { userId: request.user.id },
        order: { createdAt: "DESC" }
      });

      response.json({ learningPaths: items.map(toResponse) });
    } catch (error) {
      console.error("Failed to load learning paths.", error);
      response.status(500).json({
        message: "Failed to load learning paths",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Returns a single learning path by id.
   */
  const getLearningPath: LearningPathController["getLearningPath"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const itemId = parseId(request.params?.id);

    if (!itemId) {
      response.status(400).json({ message: "Invalid learning path id" });
      return;
    }

    try {
      const item = await repository.findOne({
        where: { id: itemId, userId: request.user.id }
      });

      if (!item) {
        response.status(404).json({ message: "Learning path not found" });
        return;
      }

      response.json({ learningPath: toResponse(item) });
    } catch (error) {
      console.error("Failed to load learning path.", error);
      response.status(500).json({
        message: "Failed to load learning path",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Creates a new learning path for the authenticated user.
   */
  const createLearningPath: LearningPathController["createLearningPath"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const payload = request.body as LearningPathPayload;
    const level = typeof payload?.level === "string" ? payload.level.trim() : "";
    const vocabulary = typeof payload?.vocabulary === "string" ? payload.vocabulary.trim() : "";

    if (!level) {
      response.status(400).json({ message: "Level is required" });
      return;
    }

    if (!vocabulary) {
      response.status(400).json({ message: "Vocabulary is required" });
      return;
    }

    try {
      const item = repository.create({
        level,
        vocabulary,
        userId: request.user.id
      });

      const saved = await repository.save(item);
      response.status(201).json({ learningPath: toResponse(saved) });
    } catch (error) {
      console.error("Failed to create learning path.", error);
      response.status(500).json({
        message: "Failed to create learning path",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Updates an existing learning path for the authenticated user.
   */
  const updateLearningPath: LearningPathController["updateLearningPath"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const itemId = parseId(request.params?.id);

    if (!itemId) {
      response.status(400).json({ message: "Invalid learning path id" });
      return;
    }

    const payload = request.body as LearningPathPayload;
    const level = typeof payload?.level === "string" ? payload.level.trim() : "";
    const vocabulary = typeof payload?.vocabulary === "string" ? payload.vocabulary.trim() : "";

    if (!level && !vocabulary) {
      response.status(400).json({ message: "No learning path fields to update" });
      return;
    }

    if (payload?.level !== undefined && !level) {
      response.status(400).json({ message: "Level cannot be empty" });
      return;
    }

    if (payload?.vocabulary !== undefined && !vocabulary) {
      response.status(400).json({ message: "Vocabulary cannot be empty" });
      return;
    }

    try {
      const item = await repository.findOne({
        where: { id: itemId, userId: request.user.id }
      });

      if (!item) {
        response.status(404).json({ message: "Learning path not found" });
        return;
      }

      if (payload?.level !== undefined) {
        item.level = level;
      }

      if (payload?.vocabulary !== undefined) {
        item.vocabulary = vocabulary;
      }

      const saved = await repository.save(item);
      response.json({ learningPath: toResponse(saved) });
    } catch (error) {
      console.error("Failed to update learning path.", error);
      response.status(500).json({
        message: "Failed to update learning path",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Deletes a learning path for the authenticated user.
   */
  const deleteLearningPath: LearningPathController["deleteLearningPath"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const itemId = parseId(request.params?.id);

    if (!itemId) {
      response.status(400).json({ message: "Invalid learning path id" });
      return;
    }

    try {
      const item = await repository.findOne({
        where: { id: itemId, userId: request.user.id }
      });

      if (!item) {
        response.status(404).json({ message: "Learning path not found" });
        return;
      }

      await repository.remove(item);
      response.json({ ok: true });
    } catch (error) {
      console.error("Failed to delete learning path.", error);
      response.status(500).json({
        message: "Failed to delete learning path",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return {
    listLearningPaths,
    getLearningPath,
    createLearningPath,
    updateLearningPath,
    deleteLearningPath
  };
};
