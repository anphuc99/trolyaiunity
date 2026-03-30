import { Router } from "express";
import type { DataSource } from "typeorm";
import { createCharacterRelationshipsController } from "../controllers/character-relationships/character-relationships.controller.js";
import { requireAuth } from "../middleware/auth.middleware.js";

/**
 * Registers routes for the CharacterRelationships resource.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns An Express router for character relationships.
 */
export const createCharacterRelationshipsRoutes = (dataSource: DataSource) => {
  const router = Router();
  const controller = createCharacterRelationshipsController(dataSource);

  router.get("/", requireAuth, controller.listRelationships);
  router.post("/", requireAuth, controller.createRelationship);
  router.post("/evaluate-session", requireAuth, controller.evaluateSession);
  router.put("/:id", requireAuth, controller.updateRelationship);
  router.delete("/:id", requireAuth, controller.deleteRelationship);

  return router;
};
