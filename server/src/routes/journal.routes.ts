import { Router } from "express";
import type { DataSource } from "typeorm";
import { createJournalController } from "../controllers/journal/journal.controller.js";
import { requireAuth } from "../middleware/auth.middleware.js";

/**
 * Registers routes for the Journal view group.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns An Express router for the Journal group.
 */
export const createJournalRoutes = (dataSource: DataSource) => {
  const router = Router();
  const controller = createJournalController(dataSource);

  router.get("/", requireAuth, controller.listJournals);
  router.get("/search", requireAuth, controller.searchMessages);
  router.get("/:id", requireAuth, controller.getJournal);
  router.post("/end", requireAuth, controller.endConversation);

  return router;
};
