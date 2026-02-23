import { Router } from "express";
import type { DataSource } from "typeorm";
import { createLevelsController } from "../controllers/levels/levels.controller.js";

/**
 * Registers routes for the Levels view group.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns An Express router for levels.
 */
export const createLevelsRoutes = (dataSource: DataSource) => {
  const router = Router();
  const controller = createLevelsController(dataSource);

  router.get("/", controller.getLevels);

  return router;
};
