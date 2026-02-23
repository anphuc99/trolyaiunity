import { Router } from "express";
import type { DataSource } from "typeorm";
import { createUsersController } from "../controllers/users/users.controller.js";
import { requireAuth } from "../middleware/auth.middleware.js";

/**
 * Registers routes for the Users view group.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns An Express router for Users.
 */
export const createUsersRoutes = (dataSource: DataSource) => {
  const router = Router();
  const controller = createUsersController(dataSource);

  router.post("/register", controller.register);
  router.post("/login", controller.login);
  router.post("/reset-password", controller.resetPassword);
  router.get("/me", requireAuth, controller.getMe);
  router.put("/level", requireAuth, controller.updateLevel);

  return router;
};
