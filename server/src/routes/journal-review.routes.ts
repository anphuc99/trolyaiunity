import { Router } from "express";
import type { DataSource } from "typeorm";
import { createJournalReviewController } from "../controllers/journal-review/journal-review.controller.js";
import { requireAuth } from "../middleware/auth.middleware.js";

/**
 * Registers routes for FSRS-based journal review.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns An Express router for journal reviews.
 */
export const createJournalReviewRoutes = (dataSource: DataSource) => {
  const router = Router();
  const controller = createJournalReviewController(dataSource);

  router.use(requireAuth);

  router.get("/due", controller.getDueJournals);
  router.post("/review", controller.reviewJournal);

  return router;
};
