import { Router } from "express";
import type { DataSource } from "typeorm";
import { createAuthRoutes } from "./auth.routes.js";
import { createUsersRoutes } from "./users.routes.js";
import { createSubjectsRoutes } from "./subjects.routes.js";
import { createKnowledgesRoutes } from "./knowledges.routes.js";
import { createCharactersRoutes } from "./characters.routes.js";
import { createChatRoutes } from "./chat.routes.js";
import { createJournalRoutes } from "./journals.routes.js";

/**
 * Creates the root API router with all route groups.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns The configured API router.
 */
export const createApiRouter = (dataSource: DataSource): Router => {
  const router = Router();

  router.use("/auth", createAuthRoutes(dataSource));
  router.use("/users", createUsersRoutes(dataSource));
  router.use("/subjects", createSubjectsRoutes(dataSource));
  router.use("/knowledges", createKnowledgesRoutes(dataSource));
  router.use("/characters", createCharactersRoutes(dataSource));
  router.use("/chat", createChatRoutes(dataSource));
  router.use("/journals", createJournalRoutes(dataSource));

  return router;
};
