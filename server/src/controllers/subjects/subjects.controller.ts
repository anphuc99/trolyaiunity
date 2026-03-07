import type { Request, Response } from "express";
import type { DataSource } from "typeorm";
import SubjectEntity from "../../models/subject.entity.js";

interface SubjectsController {
  listSubjects: (request: Request, response: Response) => Promise<void>;
  createSubject: (request: Request, response: Response) => Promise<void>;
  updateSubject: (request: Request, response: Response) => Promise<void>;
  deleteSubject: (request: Request, response: Response) => Promise<void>;
}

interface SubjectResponse {
  id: number;
  name: string;
  description: string | null;
  createdAt: string;
  updatedAt: string;
}

const toResponse = (entity: SubjectEntity): SubjectResponse => ({
  id: entity.id,
  name: entity.name,
  description: entity.description ?? null,
  createdAt: entity.createdAt.toISOString(),
  updatedAt: entity.updatedAt.toISOString()
});

const parseId = (value: string) => {
  const id = Number.parseInt(value, 10);
  return Number.isNaN(id) ? null : id;
};

/**
 * Builds the Subjects controller with injected data source.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns The Subjects controller handlers.
 */
export const createSubjectsController = (dataSource: DataSource): SubjectsController => {
  const repository = dataSource.getRepository(SubjectEntity);

  /**
   * Lists all subjects for the authenticated user.
   */
  const listSubjects: SubjectsController["listSubjects"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const subjects = await repository.find({
        where: { userId: request.user.id },
        order: { createdAt: "DESC" }
      });

      response.json(subjects.map(toResponse));
    } catch (error) {
      console.error("Failed to load subjects.", error);
      response.status(500).json({
        message: "Failed to load subjects",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Creates a new subject.
   */
  const createSubject: SubjectsController["createSubject"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const body = request.body as Record<string, unknown>;
    const name = typeof body.name === "string" ? body.name.trim() : "";
    const description = typeof body.description === "string" ? body.description.trim() : null;

    if (!name) {
      response.status(400).json({ message: "Subject name is required" });
      return;
    }

    try {
      const subject = repository.create({
        name,
        description: description || null,
        userId: request.user.id
      });

      const saved = await repository.save(subject);
      response.status(201).json(toResponse(saved));
    } catch (error) {
      console.error("Failed to create subject.", error);
      response.status(500).json({
        message: "Failed to create subject",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Updates an existing subject.
   */
  const updateSubject: SubjectsController["updateSubject"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const id = parseId(request.params.id);
    if (!id) {
      response.status(400).json({ message: "Invalid subject ID" });
      return;
    }

    const body = request.body as Record<string, unknown>;

    try {
      const subject = await repository.findOne({
        where: { id, userId: request.user.id }
      });

      if (!subject) {
        response.status(404).json({ message: "Subject not found" });
        return;
      }

      if (typeof body.name === "string") {
        const name = body.name.trim();
        if (!name) {
          response.status(400).json({ message: "Subject name cannot be empty" });
          return;
        }
        subject.name = name;
      }

      if (typeof body.description === "string") {
        subject.description = body.description.trim() || null;
      }

      const saved = await repository.save(subject);
      response.json(toResponse(saved));
    } catch (error) {
      console.error("Failed to update subject.", error);
      response.status(500).json({
        message: "Failed to update subject",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Deletes a subject and all its associated knowledges.
   */
  const deleteSubject: SubjectsController["deleteSubject"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const id = parseId(request.params.id);
    if (!id) {
      response.status(400).json({ message: "Invalid subject ID" });
      return;
    }

    try {
      const subject = await repository.findOne({
        where: { id, userId: request.user.id }
      });

      if (!subject) {
        response.status(404).json({ message: "Subject not found" });
        return;
      }

      await repository.remove(subject);
      response.json({ message: "Subject deleted" });
    } catch (error) {
      console.error("Failed to delete subject.", error);
      response.status(500).json({
        message: "Failed to delete subject",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return { listSubjects, createSubject, updateSubject, deleteSubject };
};
