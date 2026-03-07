import { Router } from "express";
import type { DataSource } from "typeorm";
import { createUsersController } from "../controllers/users/users.controller.js";
import { requireAuth } from "../middleware/auth.middleware.js";

/**
 * Registers routes for the Users resource.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns An Express router for Users.
 */
export const createUsersRoutes = (dataSource: DataSource) => {
  const router = Router();
  const controller = createUsersController(dataSource);

  router.use(requireAuth);

  router.get("/me", controller.getMe);
  router.put("/me", controller.updateMe);

  return router;
};
