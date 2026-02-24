import { Router } from "express";
import type { DataSource } from "typeorm";
import { createHealthController } from "../controllers/shared/health.controller.js";
import { createTokenController } from "../controllers/shared/token.controller.js";
import { createTtsController } from "../controllers/shared/tts.controller.js";
import { requireAuth } from "../middleware/auth.middleware.js";

/**
 * Registers shared API routes not tied to a view group.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns An Express router for shared endpoints.
 */
export const createSharedRoutes = (dataSource: DataSource) => {
  const router = Router();
  const controller = createHealthController();
  const tokenController = createTokenController();
  const ttsController = createTtsController(dataSource);

  router.get("/health", controller.getHealth);
  router.post("/token/validate", tokenController.validateToken);
  router.post("/token/refresh", tokenController.refreshToken);
  router.get("/text-to-speech", requireAuth, ttsController.getTextToSpeech);

  return router;
};
