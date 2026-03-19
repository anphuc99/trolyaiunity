import { Router } from "express";
import type { DataSource } from "typeorm";
import { createLearningPathController } from "../controllers/learning-path/learning-path.controller.js";
import { requireAuth } from "../middleware/auth.middleware.js";

/**
 * Registers routes for the LearningPath feature.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns An Express router for LearningPath.
 */
export const createLearningPathRoutes = (dataSource: DataSource) => {
  const router = Router();
  const controller = createLearningPathController(dataSource);

  router.use(requireAuth);

  router.get("/", controller.listLearningPaths);
  router.get("/:id", controller.getLearningPath);
  router.post("/", controller.createLearningPath);
  router.put("/:id", controller.updateLearningPath);
  router.delete("/:id", controller.deleteLearningPath);

  return router;
};
