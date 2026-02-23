import { Router } from "express";
import type { DataSource } from "typeorm";
import { createTasksController } from "../controllers/tasks/tasks.controller.js";
import { requireAuth } from "../middleware/auth.middleware.js";

/**
 * Registers routes for the Tasks view group.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns An Express router for Tasks.
 */
export const createTasksRoutes = (dataSource: DataSource) => {
  const router = Router();
  const controller = createTasksController(dataSource);

  router.use(requireAuth);

  router.get("/today", controller.getTodayTasks);

  return router;
};
