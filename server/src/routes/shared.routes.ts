import { Router } from "express";
import { createHealthController } from "../controllers/shared/health.controller.js";
import { createTokenController } from "../controllers/shared/token.controller.js";
import { createTtsController } from "../controllers/shared/tts.controller.js";

/**
 * Registers shared API routes not tied to a view group.
 *
 * @returns An Express router for shared endpoints.
 */
export const createSharedRoutes = () => {
  const router = Router();
  const controller = createHealthController();
  const tokenController = createTokenController();
  const ttsController = createTtsController();

  router.get("/health", controller.getHealth);
  router.post("/token/validate", tokenController.validateToken);
  router.post("/token/refresh", tokenController.refreshToken);
  router.get("/text-to-speech", ttsController.getTextToSpeech);

  return router;
};
