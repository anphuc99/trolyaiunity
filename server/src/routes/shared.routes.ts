import { Router } from "express";
import multer from "multer";
import type { DataSource } from "typeorm";
import { createHealthController } from "../controllers/shared/health.controller.js";
import { createTokenController } from "../controllers/shared/token.controller.js";
import { createTtsController } from "../controllers/shared/tts.controller.js";
import { createVoicesController } from "../controllers/shared/voices.controller.js";
import { requireAuth } from "../middleware/auth.middleware.js";
import type { ReferenceAudioVectorService } from "../services/reference-audio-vector.service.js";

const upload = multer({ storage: multer.memoryStorage(), limits: { fileSize: 50 * 1024 * 1024 } });

/**
 * Registers shared API routes not tied to a view group.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @param refAudioService - Optional reference audio vector service for GPT-SoVITS.
 * @returns An Express router for shared endpoints.
 */
export const createSharedRoutes = (dataSource: DataSource, refAudioService?: ReferenceAudioVectorService) => {
  const router = Router();
  const controller = createHealthController();
  const tokenController = createTokenController();
  const ttsController = createTtsController(dataSource, refAudioService);
  const voicesController = createVoicesController(dataSource);

  router.get("/health", controller.getHealth);
  router.post("/token/validate", tokenController.validateToken);
  router.post("/token/refresh", tokenController.refreshToken);
  router.get("/voices", requireAuth, voicesController.listVoices);
  router.get("/text-to-speech", requireAuth, ttsController.getTextToSpeech);
  router.get("/check-audio", requireAuth, ttsController.checkAudioExists);
  router.get("/reference-audio", requireAuth, ttsController.getReferenceAudio);
  router.post("/process-tts-wav", requireAuth, upload.single("audio"), ttsController.processTtsWav);

  return router;
};
