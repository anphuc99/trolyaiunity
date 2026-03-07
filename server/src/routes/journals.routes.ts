import { Router } from "express";
import type { DataSource } from "typeorm";
import { requireAuth } from "../middleware/auth.middleware.js";
import { createJournalController } from "../controllers/journals/journals.controller.js";

/**
 * Creates journal routes.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns Express Router for journal endpoints.
 */
export const createJournalRoutes = (dataSource: DataSource): Router => {
  const router = Router();
  const controller = createJournalController(dataSource);

  /** GET /journals — list all journals for the current user */
  router.get("/", requireAuth, controller.listJournals);

  /** GET /journals/messages/search?q=keyword — search messages by content */
  router.get("/messages/search", requireAuth, controller.searchMessages);

  /** GET /journals/:id — get a single journal with messages */
  router.get("/:id", requireAuth, controller.getJournal);

  /** POST /journals/end — end current conversation, save as journal */
  router.post("/end", requireAuth, controller.endConversation);

  return router;
};
