import type { Request, Response } from "express";
import type { DataSource } from "typeorm";
import KnowledgeEntity from "../../models/knowledge.entity.js";
import KnowledgeReviewEntity from "../../models/knowledge-review.entity.js";
import SubjectEntity from "../../models/subject.entity.js";
import { createInitialReviewState } from "../../services/fsrs.service.js";

interface KnowledgesController {
  listKnowledges: (request: Request, response: Response) => Promise<void>;
  createKnowledge: (request: Request, response: Response) => Promise<void>;
  updateKnowledge: (request: Request, response: Response) => Promise<void>;
  deleteKnowledge: (request: Request, response: Response) => Promise<void>;
}

interface KnowledgeResponse {
  id: number;
  name: string;
  description: string | null;
  subjectId: number;
  createdAt: string;
  updatedAt: string;
}

const toResponse = (entity: KnowledgeEntity): KnowledgeResponse => ({
  id: entity.id,
  name: entity.name,
  description: entity.description ?? null,
  subjectId: entity.subjectId,
  createdAt: entity.createdAt.toISOString(),
  updatedAt: entity.updatedAt.toISOString()
});

const parseId = (value: string) => {
  const id = Number.parseInt(value, 10);
  return Number.isNaN(id) ? null : id;
};

/**
 * Builds the Knowledges controller with injected data source.
 *
 * When a knowledge item is created, an FSRS review entry is automatically
 * initialized so the item appears in the review queue.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns The Knowledges controller handlers.
 */
export const createKnowledgesController = (dataSource: DataSource): KnowledgesController => {
  const knowledgeRepository = dataSource.getRepository(KnowledgeEntity);
  const reviewRepository = dataSource.getRepository(KnowledgeReviewEntity);
  const subjectRepository = dataSource.getRepository(SubjectEntity);

  /**
   * Lists all knowledges for a subject.
   */
  const listKnowledges: KnowledgesController["listKnowledges"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const subjectId = parseId(request.params.subjectId);
    if (!subjectId) {
      response.status(400).json({ message: "Invalid subject ID" });
      return;
    }

    try {
      // Verify subject belongs to user
      const subject = await subjectRepository.findOne({
        where: { id: subjectId, userId: request.user.id }
      });

      if (!subject) {
        response.status(404).json({ message: "Subject not found" });
        return;
      }

      const knowledges = await knowledgeRepository.find({
        where: { subjectId, userId: request.user.id },
        order: { createdAt: "DESC" }
      });

      response.json(knowledges.map(toResponse));
    } catch (error) {
      console.error("Failed to load knowledges.", error);
      response.status(500).json({
        message: "Failed to load knowledges",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Creates a new knowledge item and initializes its FSRS review state.
   */
  const createKnowledge: KnowledgesController["createKnowledge"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const subjectId = parseId(request.params.subjectId);
    if (!subjectId) {
      response.status(400).json({ message: "Invalid subject ID" });
      return;
    }

    const body = request.body as Record<string, unknown>;
    const name = typeof body.name === "string" ? body.name.trim() : "";
    const description = typeof body.description === "string" ? body.description.trim() : null;

    if (!name) {
      response.status(400).json({ message: "Knowledge name is required" });
      return;
    }

    try {
      // Verify subject belongs to user
      const subject = await subjectRepository.findOne({
        where: { id: subjectId, userId: request.user.id }
      });

      if (!subject) {
        response.status(404).json({ message: "Subject not found" });
        return;
      }

      const knowledge = knowledgeRepository.create({
        name,
        description: description || null,
        subjectId,
        userId: request.user.id
      });

      const saved = await knowledgeRepository.save(knowledge);

      // Auto-create FSRS review entry
      const initialState = createInitialReviewState();
      const review = reviewRepository.create({
        knowledgeId: saved.id,
        userId: request.user.id,
        stability: initialState.stability,
        difficulty: initialState.difficulty,
        lapses: initialState.lapses,
        currentIntervalDays: initialState.currentIntervalDays,
        nextReviewDate: new Date(initialState.nextReviewDate),
        lastReviewDate: null,
        reviewHistoryJson: JSON.stringify(initialState.reviewHistory)
      });

      await reviewRepository.save(review);

      response.status(201).json(toResponse(saved));
    } catch (error) {
      console.error("Failed to create knowledge.", error);
      response.status(500).json({
        message: "Failed to create knowledge",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Updates an existing knowledge item.
   */
  const updateKnowledge: KnowledgesController["updateKnowledge"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const id = parseId(request.params.id);
    if (!id) {
      response.status(400).json({ message: "Invalid knowledge ID" });
      return;
    }

    const body = request.body as Record<string, unknown>;

    try {
      const knowledge = await knowledgeRepository.findOne({
        where: { id, userId: request.user.id }
      });

      if (!knowledge) {
        response.status(404).json({ message: "Knowledge not found" });
        return;
      }

      if (typeof body.name === "string") {
        const name = body.name.trim();
        if (!name) {
          response.status(400).json({ message: "Knowledge name cannot be empty" });
          return;
        }
        knowledge.name = name;
      }

      if (typeof body.description === "string") {
        knowledge.description = body.description.trim() || null;
      }

      const saved = await knowledgeRepository.save(knowledge);
      response.json(toResponse(saved));
    } catch (error) {
      console.error("Failed to update knowledge.", error);
      response.status(500).json({
        message: "Failed to update knowledge",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Deletes a knowledge item (and its associated review via CASCADE).
   */
  const deleteKnowledge: KnowledgesController["deleteKnowledge"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const id = parseId(request.params.id);
    if (!id) {
      response.status(400).json({ message: "Invalid knowledge ID" });
      return;
    }

    try {
      const knowledge = await knowledgeRepository.findOne({
        where: { id, userId: request.user.id }
      });

      if (!knowledge) {
        response.status(404).json({ message: "Knowledge not found" });
        return;
      }

      await knowledgeRepository.remove(knowledge);
      response.json({ message: "Knowledge deleted" });
    } catch (error) {
      console.error("Failed to delete knowledge.", error);
      response.status(500).json({
        message: "Failed to delete knowledge",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return { listKnowledges, createKnowledge, updateKnowledge, deleteKnowledge };
};
