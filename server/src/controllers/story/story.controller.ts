import type { Request, Response } from "express";
import type { DataSource } from "typeorm";
import StoryEntity from "../../models/story.entity.js";

interface StoryPayload {
  name?: string;
  description?: string;
  currentProgress?: string | null;
}

interface StoryResponse {
  id: number;
  name: string;
  description: string;
  currentProgress: string | null;
  createdAt: string;
  updatedAt: string;
}

interface StoryController {
  listStories: (request: Request, response: Response) => Promise<void>;
  getStory: (request: Request, response: Response) => Promise<void>;
  createStory: (request: Request, response: Response) => Promise<void>;
  updateStory: (request: Request, response: Response) => Promise<void>;
  deleteStory: (request: Request, response: Response) => Promise<void>;
}

const parseId = (value: string) => {
  const id = Number.parseInt(value, 10);
  return Number.isNaN(id) ? null : id;
};

const normalizeOptionalText = (value: unknown) => {
  if (typeof value !== "string") {
    return null;
  }

  const trimmed = value.trim();
  return trimmed ? trimmed : null;
};

const toResponse = (entity: StoryEntity): StoryResponse => ({
  id: entity.id,
  name: entity.name,
  description: entity.description,
  currentProgress: entity.currentProgress ?? null,
  createdAt: entity.createdAt.toISOString(),
  updatedAt: entity.updatedAt.toISOString()
});

/**
 * Builds the Story controller with injected data source dependencies.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns The Story controller handlers.
 */
export const createStoryController = (dataSource: DataSource): StoryController => {
  const repository = dataSource.getRepository(StoryEntity);

  /**
   * Lists stories for the authenticated user.
   *
   * @param request - Express request.
   * @param response - Express response.
   */
  const listStories: StoryController["listStories"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const stories = await repository.find({
        where: { userId: request.user.id },
        order: { createdAt: "DESC" }
      });

      response.json({ stories: stories.map(toResponse) });
    } catch (error) {
      console.error("Failed to load stories.", error);
      response.status(500).json({
        message: "Failed to load stories",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Returns a single story by id.
   *
   * @param request - Express request.
   * @param response - Express response.
   */
  const getStory: StoryController["getStory"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const storyId = parseId(request.params?.id);

    if (!storyId) {
      response.status(400).json({ message: "Invalid story id" });
      return;
    }

    try {
      const story = await repository.findOne({
        where: { id: storyId, userId: request.user.id }
      });

      if (!story) {
        response.status(404).json({ message: "Story not found" });
        return;
      }

      response.json({ story: toResponse(story) });
    } catch (error) {
      console.error("Failed to load story.", error);
      response.status(500).json({
        message: "Failed to load story",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Creates a new story for the authenticated user.
   *
   * @param request - Express request.
   * @param response - Express response.
   */
  const createStory: StoryController["createStory"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const payload = request.body as StoryPayload;
    const name = typeof payload?.name === "string" ? payload.name.trim() : "";
    const description = typeof payload?.description === "string" ? payload.description.trim() : "";

    if (!name || !description) {
      response.status(400).json({ message: "Name and description are required" });
      return;
    }

    try {
      const story = repository.create({
        name,
        description,
        currentProgress: normalizeOptionalText(payload?.currentProgress),
        userId: request.user.id
      });

      const saved = await repository.save(story);
      response.status(201).json({ story: toResponse(saved) });
    } catch (error) {
      console.error("Failed to create story.", error);
      response.status(500).json({
        message: "Failed to create story",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Updates an existing story for the authenticated user.
   *
   * @param request - Express request.
   * @param response - Express response.
   */
  const updateStory: StoryController["updateStory"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const storyId = parseId(request.params?.id);

    if (!storyId) {
      response.status(400).json({ message: "Invalid story id" });
      return;
    }

    const payload = request.body as StoryPayload;
    const name = typeof payload?.name === "string" ? payload.name.trim() : "";
    const description = typeof payload?.description === "string" ? payload.description.trim() : "";
    const currentProgress = normalizeOptionalText(payload?.currentProgress);

    if (!name && !description && currentProgress === null) {
      response.status(400).json({ message: "No story fields to update" });
      return;
    }

    if (payload?.name !== undefined && !name) {
      response.status(400).json({ message: "Story name cannot be empty" });
      return;
    }

    if (payload?.description !== undefined && !description) {
      response.status(400).json({ message: "Story description cannot be empty" });
      return;
    }

    try {
      const story = await repository.findOne({
        where: { id: storyId, userId: request.user.id }
      });

      if (!story) {
        response.status(404).json({ message: "Story not found" });
        return;
      }

      if (payload?.name !== undefined) {
        story.name = name;
      }

      if (payload?.description !== undefined) {
        story.description = description;
      }

      if (payload?.currentProgress !== undefined) {
        story.currentProgress = currentProgress;
      }

      const saved = await repository.save(story);
      response.json({ story: toResponse(saved) });
    } catch (error) {
      console.error("Failed to update story.", error);
      response.status(500).json({
        message: "Failed to update story",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Deletes a story for the authenticated user.
   *
   * @param request - Express request.
   * @param response - Express response.
   */
  const deleteStory: StoryController["deleteStory"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const storyId = parseId(request.params?.id);

    if (!storyId) {
      response.status(400).json({ message: "Invalid story id" });
      return;
    }

    try {
      const story = await repository.findOne({
        where: { id: storyId, userId: request.user.id }
      });

      if (!story) {
        response.status(404).json({ message: "Story not found" });
        return;
      }

      await repository.remove(story);
      response.json({ ok: true });
    } catch (error) {
      console.error("Failed to delete story.", error);
      response.status(500).json({
        message: "Failed to delete story",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return {
    listStories,
    getStory,
    createStory,
    updateStory,
    deleteStory
  };
};
