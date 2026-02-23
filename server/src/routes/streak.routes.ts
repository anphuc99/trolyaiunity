import { Router } from "express";
import type { DataSource } from "typeorm";
import { createStreakController } from "../controllers/streak/streak.controller.js";
import { requireAuth } from "../middleware/auth.middleware.js";

/**
 * Registers routes for the Streak view group.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns An Express router for Streak.
 */
export const createStreakRoutes = (dataSource: DataSource) => {
  const router = Router();
  const controller = createStreakController(dataSource);

  router.use(requireAuth);

  router.get("/", controller.getStreak);

  return router;
};
