import { Router } from "express";
import type { DataSource } from "typeorm";
import { createChatRoutes } from "./chat.routes.js";
import { createHomeRoutes } from "./home.routes.js";
import { createSharedRoutes } from "./shared.routes.js";
// mvc-gen:imports
import { createCharactersRoutes } from "./characters.routes.js";
import { createJournalRoutes } from "./journal.routes.js";
import { createLevelsRoutes } from "./levels.routes.js";
import { createStreakRoutes } from "./streak.routes.js";
import { createStoryRoutes } from "./story.routes.js";
import { createTasksRoutes } from "./tasks.routes.js";
import { createTranslationRoutes } from "./translation.routes.js";
import { createUsersRoutes } from "./users.routes.js";
import { createVocabularyRoutes } from "./vocabulary.routes.js";

/**
 * Creates the root API router with all route groups.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns The configured API router.
 */
export const createApiRouter = (dataSource: DataSource) => {
  const router = Router();

  // mvc-gen:routes
  router.use("/characters", createCharactersRoutes(dataSource));
  router.use("/chat", createChatRoutes(dataSource));
  router.use("/home", createHomeRoutes(dataSource));
  router.use("/journals", createJournalRoutes(dataSource));
  router.use("/levels", createLevelsRoutes(dataSource));
  router.use("/streak", createStreakRoutes(dataSource));
  router.use("/stories", createStoryRoutes(dataSource));
  router.use("/tasks", createTasksRoutes(dataSource));
  router.use("/translation", createTranslationRoutes(dataSource));
  router.use("/users", createUsersRoutes(dataSource));
  router.use("/vocabulary", createVocabularyRoutes(dataSource));
  router.use("/", createSharedRoutes());

  return router;
};
