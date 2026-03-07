import { Router } from "express";
import type { DataSource } from "typeorm";
import { createKnowledgesController } from "../controllers/knowledges/knowledges.controller.js";
import { createKnowledgeReviewsController } from "../controllers/knowledge-reviews/knowledge-reviews.controller.js";
import { requireAuth } from "../middleware/auth.middleware.js";

/**
 * Registers routes for the Knowledges resource.
 *
 * Includes both CRUD endpoints (nested under subjects) and
 * FSRS review endpoints for spaced repetition.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns An Express router for Knowledges.
 */
export const createKnowledgesRoutes = (dataSource: DataSource) => {
  const router = Router();
  const knowledgesController = createKnowledgesController(dataSource);
  const reviewsController = createKnowledgeReviewsController(dataSource);

  router.use(requireAuth);

  // FSRS review endpoints (must be before /:id to avoid route conflicts)
  router.get("/review/due", reviewsController.getDueReviews);
  router.get("/review/stats", reviewsController.getReviewStats);
  router.post("/:id/review", reviewsController.submitReview);

  // CRUD endpoints
  router.put("/:id", knowledgesController.updateKnowledge);
  router.delete("/:id", knowledgesController.deleteKnowledge);

  return router;
};

/**
 * Registers routes for listing/creating knowledges under a subject.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns An Express router for Subject-scoped Knowledges.
 */
export const createSubjectKnowledgesRoutes = (dataSource: DataSource) => {
  const router = Router({ mergeParams: true });
  const controller = createKnowledgesController(dataSource);

  router.use(requireAuth);

  router.get("/", controller.listKnowledges);
  router.post("/", controller.createKnowledge);

  return router;
};
