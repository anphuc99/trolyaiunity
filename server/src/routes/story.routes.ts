import { Router } from "express";
import type { DataSource } from "typeorm";
import { createStoryController } from "../controllers/story/story.controller.js";
import { requireAuth } from "../middleware/auth.middleware.js";

/**
 * Registers routes for the Story view group.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns An Express router for Story.
 */
export const createStoryRoutes = (dataSource: DataSource) => {
  const router = Router();
  const controller = createStoryController(dataSource);

  router.use(requireAuth);

  router.get("/", controller.listStories);
  router.get("/:id", controller.getStory);
  router.post("/", controller.createStory);
  router.put("/:id", controller.updateStory);
  router.delete("/:id", controller.deleteStory);

  return router;
};
