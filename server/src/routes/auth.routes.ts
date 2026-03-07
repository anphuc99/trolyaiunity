import { Router } from "express";
import type { DataSource } from "typeorm";
import { createAuthController } from "../controllers/auth/auth.controller.js";

/**
 * Registers routes for authentication (no auth required).
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns An Express router for Auth.
 */
export const createAuthRoutes = (dataSource: DataSource) => {
  const router = Router();
  const controller = createAuthController(dataSource);

  router.post("/register", controller.register);
  router.post("/login", controller.login);
  router.post("/token/refresh", controller.refreshToken);

  return router;
};
