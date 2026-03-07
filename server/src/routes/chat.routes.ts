import { Router } from "express";
import type { DataSource } from "typeorm";
import { createChatController } from "../controllers/chat/chat.controller.js";
import { requireAuth } from "../middleware/auth.middleware.js";

/**
 * Registers routes for the Chat resource.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns An Express router for Chat.
 */
export const createChatRoutes = (dataSource: DataSource) => {
  const router = Router();
  const controller = createChatController(dataSource);

  router.use(requireAuth);

  router.get("/history", controller.getHistory);
  router.post("/send", controller.sendMessage);
  router.post("/clear", controller.clearHistory);

  return router;
};
