import { Router } from "express";
import type { DataSource } from "typeorm";
import { createCharactersController } from "../controllers/characters/characters.controller.js";
import { requireAuth } from "../middleware/auth.middleware.js";

/**
 * Registers routes for the Characters view group.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns An Express router for Characters.
 */
export const createCharactersRoutes = (dataSource: DataSource) => {
  const router = Router();
  const controller = createCharactersController(dataSource);

  router.use(requireAuth);

  router.post("/upload-avatar", controller.uploadAvatar);
  router.get("/", controller.listCharacters);
  router.post("/", controller.createCharacter);
  router.put("/:id", controller.updateCharacter);
  router.delete("/:id", controller.deleteCharacter);

  return router;
};
