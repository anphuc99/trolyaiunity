import { Router } from "express";
import type { DataSource } from "typeorm";
import { createSubjectsController } from "../controllers/subjects/subjects.controller.js";
import { requireAuth } from "../middleware/auth.middleware.js";

/**
 * Registers routes for the Subjects resource.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns An Express router for Subjects.
 */
export const createSubjectsRoutes = (dataSource: DataSource) => {
  const router = Router();
  const controller = createSubjectsController(dataSource);

  router.use(requireAuth);

  router.get("/", controller.listSubjects);
  router.post("/", controller.createSubject);
  router.put("/:id", controller.updateSubject);
  router.delete("/:id", controller.deleteSubject);

  return router;
};
