import type { Request, Response } from "express";
import type { DataSource } from "typeorm";
import { LessThanOrEqual } from "typeorm";
import KnowledgeReviewEntity from "../../models/knowledge-review.entity.js";
import KnowledgeEntity from "../../models/knowledge.entity.js";
import {
  updateReviewAfterRating,
  type FSRSRating,
  type ReviewHistoryEntry,
  type ReviewState
} from "../../services/fsrs.service.js";

interface KnowledgeReviewsController {
  getDueReviews: (request: Request, response: Response) => Promise<void>;
  submitReview: (request: Request, response: Response) => Promise<void>;
  getReviewStats: (request: Request, response: Response) => Promise<void>;
}

const parseId = (value: string) => {
  const id = Number.parseInt(value, 10);
  return Number.isNaN(id) ? null : id;
};

/**
 * Converts a review entity to the ReviewState format used by the FSRS service.
 */
const entityToReviewState = (entity: KnowledgeReviewEntity): ReviewState => {
  let reviewHistory: ReviewHistoryEntry[] = [];
  try {
    reviewHistory = JSON.parse(entity.reviewHistoryJson) as ReviewHistoryEntry[];
  } catch {
    reviewHistory = [];
  }

  return {
    stability: entity.stability,
    difficulty: entity.difficulty,
    lapses: entity.lapses,
    currentIntervalDays: entity.currentIntervalDays,
    nextReviewDate: entity.nextReviewDate.toISOString(),
    lastReviewDate: entity.lastReviewDate?.toISOString() ?? null,
    reviewHistory
  };
};

/**
 * Builds the Knowledge Reviews controller.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns The Knowledge Reviews controller handlers.
 */
export const createKnowledgeReviewsController = (dataSource: DataSource): KnowledgeReviewsController => {
  const reviewRepository = dataSource.getRepository(KnowledgeReviewEntity);
  const knowledgeRepository = dataSource.getRepository(KnowledgeEntity);

  /**
   * Returns knowledge reviews that are due for review (nextReviewDate <= now).
   */
  const getDueReviews: KnowledgeReviewsController["getDueReviews"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const now = new Date();
      const reviews = await reviewRepository.find({
        where: {
          userId: request.user.id,
          nextReviewDate: LessThanOrEqual(now)
        },
        relations: ["knowledge"],
        order: { nextReviewDate: "ASC" }
      });

      const result = reviews.map((review) => ({
        id: review.id,
        knowledgeId: review.knowledgeId,
        knowledgeName: review.knowledge?.name ?? "",
        knowledgeDescription: review.knowledge?.description ?? null,
        stability: review.stability,
        difficulty: review.difficulty,
        lapses: review.lapses,
        currentIntervalDays: review.currentIntervalDays,
        nextReviewDate: review.nextReviewDate.toISOString(),
        lastReviewDate: review.lastReviewDate?.toISOString() ?? null
      }));

      response.json(result);
    } catch (error) {
      console.error("Failed to load due reviews.", error);
      response.status(500).json({
        message: "Failed to load due reviews",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Submits a review rating for a knowledge item and updates FSRS scheduling.
   */
  const submitReview: KnowledgeReviewsController["submitReview"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const knowledgeId = parseId(request.params.id);
    if (!knowledgeId) {
      response.status(400).json({ message: "Invalid knowledge ID" });
      return;
    }

    const body = request.body as Record<string, unknown>;
    const rating = typeof body.rating === "number" ? body.rating : 0;

    if (rating < 1 || rating > 4 || !Number.isInteger(rating)) {
      response.status(400).json({ message: "Rating must be 1 (Again), 2 (Hard), 3 (Good), or 4 (Easy)" });
      return;
    }

    try {
      // Verify knowledge belongs to user
      const knowledge = await knowledgeRepository.findOne({
        where: { id: knowledgeId, userId: request.user.id }
      });

      if (!knowledge) {
        response.status(404).json({ message: "Knowledge not found" });
        return;
      }

      const review = await reviewRepository.findOne({
        where: { knowledgeId, userId: request.user.id }
      });

      if (!review) {
        response.status(404).json({ message: "Review not found for this knowledge" });
        return;
      }

      const currentState = entityToReviewState(review);
      const newState = updateReviewAfterRating(currentState, rating as FSRSRating);

      review.stability = newState.stability;
      review.difficulty = newState.difficulty;
      review.lapses = newState.lapses;
      review.currentIntervalDays = newState.currentIntervalDays;
      review.nextReviewDate = new Date(newState.nextReviewDate);
      review.lastReviewDate = newState.lastReviewDate ? new Date(newState.lastReviewDate) : null;
      review.reviewHistoryJson = JSON.stringify(newState.reviewHistory);

      const saved = await reviewRepository.save(review);

      response.json({
        id: saved.id,
        knowledgeId: saved.knowledgeId,
        stability: saved.stability,
        difficulty: saved.difficulty,
        lapses: saved.lapses,
        currentIntervalDays: saved.currentIntervalDays,
        nextReviewDate: saved.nextReviewDate.toISOString(),
        lastReviewDate: saved.lastReviewDate?.toISOString() ?? null
      });
    } catch (error) {
      console.error("Failed to submit review.", error);
      response.status(500).json({
        message: "Failed to submit review",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Returns review statistics for the authenticated user.
   */
  const getReviewStats: KnowledgeReviewsController["getReviewStats"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const now = new Date();

      const total = await reviewRepository.count({
        where: { userId: request.user.id }
      });

      const due = await reviewRepository.count({
        where: {
          userId: request.user.id,
          nextReviewDate: LessThanOrEqual(now)
        }
      });

      // Items reviewed today
      const todayStart = new Date(now);
      todayStart.setHours(0, 0, 0, 0);

      const allReviews = await reviewRepository.find({
        where: { userId: request.user.id }
      });

      let reviewedToday = 0;
      let newToday = 0;

      for (const review of allReviews) {
        if (review.lastReviewDate && review.lastReviewDate >= todayStart) {
          reviewedToday++;
        }
        if (review.createdAt >= todayStart) {
          newToday++;
        }
      }

      response.json({
        total,
        due,
        reviewedToday,
        newToday
      });
    } catch (error) {
      console.error("Failed to load review stats.", error);
      response.status(500).json({
        message: "Failed to load review stats",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return { getDueReviews, submitReview, getReviewStats };
};
