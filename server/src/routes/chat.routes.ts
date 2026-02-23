import { Router } from "express";
import type { DataSource } from "typeorm";
import { createChatController } from "../controllers/chat/chat.controller.js";
import { requireAuth } from "../middleware/auth.middleware.js";

/**
 * Registers routes for the Chat view group.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns An Express router for the Chat group.
 */
export const createChatRoutes = (dataSource: DataSource) => {
  const router = Router();
  const controller = createChatController(dataSource);

  router.get("/history", requireAuth, controller.getHistory);
  router.get("/developer-state", requireAuth, controller.getDeveloperState);
  router.post("/developer", requireAuth, controller.appendDeveloperMessage);
  router.post("/edit", requireAuth, controller.editMessage);
  router.post("/send", requireAuth, controller.sendMessage);

  return router;
};
