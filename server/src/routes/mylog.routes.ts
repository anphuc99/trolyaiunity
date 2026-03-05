import { Router } from "express";
import type { DataSource } from "typeorm";
import { createMyLogController } from "../controllers/mylog/mylog.controller.js";
import { requireAuth } from "../middleware/auth.middleware.js";

/**
 * Creates the MyLog (diary) API routes.
 *
 * All routes require authentication via Bearer token.
 *
 * Routes:
 *   POST   /api/mylog              — Create a new diary entry
 *   GET    /api/mylog              — List diary entries (paginated)
 *   PUT    /api/mylog/:id          — Update an existing diary entry
 *   GET    /api/mylog/review/due   — Get diary entries due for review
 *   POST   /api/mylog/review       — Submit a review for a diary entry
 *   GET    /api/mylog/journals     — List diary chat journals
 *   GET    /api/mylog/journals/:id — Get a diary journal with messages
 *   GET    /api/mylog/chat/history         — Get current diary chat history
 *   GET    /api/mylog/chat/developer-state — Get active character names
 *   POST   /api/mylog/chat/send            — Send a message in diary chat
 *   POST   /api/mylog/chat/developer       — Append a developer message
 *   POST   /api/mylog/chat/edit            — Edit a user or assistant message
 *   POST   /api/mylog/chat/end             — End diary chat and save journal
 *   GET    /api/mylog/:id          — Get a single diary entry
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns The configured router.
 */
export const createMyLogRoutes = (dataSource: DataSource) => {
  const router = Router();
  const controller = createMyLogController(dataSource);

  // Diary CRUD
  router.post("/", requireAuth, controller.createLog);
  router.get("/", requireAuth, controller.listLogs);
  router.put("/:id", requireAuth, controller.updateLog);

  // Spaced repetition review
  router.get("/review/due", requireAuth, controller.getDueLogs);
  router.post("/review", requireAuth, controller.submitReview);

  // Diary chat journals
  router.get("/journals", requireAuth, controller.listJournals);
  router.get("/journals/:id", requireAuth, controller.getJournal);

  // Diary chat session
  router.get("/chat/history", requireAuth, controller.getHistory);
  router.get("/chat/developer-state", requireAuth, controller.getDeveloperState);
  router.post("/chat/send", requireAuth, controller.sendMessage);
  router.post("/chat/developer", requireAuth, controller.appendDeveloperMessage);
  router.post("/chat/edit", requireAuth, controller.editMessage);
  router.post("/chat/end", requireAuth, controller.endConversation);

  // Single entry (must be last to avoid matching other paths)
  router.get("/:id", requireAuth, controller.getLog);

  return router;
};
