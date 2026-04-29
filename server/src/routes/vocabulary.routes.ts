import { Router } from "express";
import type { DataSource } from "typeorm";
import { createVocabularyController } from "../controllers/vocabulary/vocabulary.controller.js";
import { requireAuth } from "../middleware/auth.middleware.js";

/**
 * Registers routes for the Vocabulary view group.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns An Express router for Vocabulary.
 */
export const createVocabularyRoutes = (dataSource: DataSource) => {
  const router = Router();
  const controller = createVocabularyController(dataSource);

  router.use(requireAuth);

  router.get("/lookup", controller.lookupWord);
  router.get("/learned-count", controller.getLearnedCount);
  router.get("/stats", controller.getStats);
  router.get("/due", controller.getDueReviews);
  router.post("/batch-review", controller.batchReviewByWords);
  router.get("/", controller.listVocabularies);
  router.get("/:id", controller.getVocabulary);
  router.post("/", controller.collectVocabulary);
  router.put("/:id", controller.updateVocabulary);
  router.delete("/:id", controller.deleteVocabulary);
  router.post("/:id/review", controller.reviewVocabulary);
  router.put("/:id/star", controller.toggleStar);
  router.put("/:id/direction", controller.setCardDirection);
  router.put("/:id/ignore", controller.ignoreVocabulary);

  return router;
};
