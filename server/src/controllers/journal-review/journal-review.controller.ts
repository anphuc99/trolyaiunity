import type { Request, Response } from "express";
import type { DataSource, Repository } from "typeorm";
import JournalEntity from "../../models/journal.entity.js";
import JournalReviewEntity from "../../models/journal-review.entity.js";
import {
  createInitialReviewState,
  updateReviewAfterRating,
  type FSRSRating,
  type ReviewHistoryEntry
} from "../../services/fsrs.service.js";

/**
 * Public handler signatures for the journal-review controller.
 */
interface JournalReviewController {
  getDueJournals: (request: Request, response: Response) => Promise<void>;
  reviewJournal: (request: Request, response: Response) => Promise<void>;
}

/**
 * Serialises a journal review entity to a client-facing JSON shape.
 *
 * @param entity - JournalReviewEntity row.
 * @returns Serialised review object.
 */
const serialiseReview = (entity: JournalReviewEntity) => {
  let reviewHistory: ReviewHistoryEntry[] = [];

  try {
    reviewHistory = JSON.parse(entity.reviewHistoryJson || "[]") as ReviewHistoryEntry[];
  } catch {
    reviewHistory = [];
  }

  return {
    id: entity.id,
    journalId: entity.journalId,
    stability: entity.stability,
    difficulty: entity.difficulty,
    lapses: entity.lapses,
    currentIntervalDays: entity.currentIntervalDays,
    nextReviewDate: entity.nextReviewDate,
    lastReviewDate: entity.lastReviewDate,
    reviewHistory
  };
};

/**
 * Builds the journal-review controller.
 *
 * @param dataSource - Initialised TypeORM data source.
 * @returns Controller handlers.
 */
export const createJournalReviewController = (dataSource: DataSource): JournalReviewController => {
  const journalRepo: Repository<JournalEntity> = dataSource.getRepository(JournalEntity);
  const reviewRepo: Repository<JournalReviewEntity> = dataSource.getRepository(JournalReviewEntity);

  /**
   * Converts a Date to a YYYY-MM-DD string in Asia/Ho_Chi_Minh timezone.
   */
  const toDateKey = (value: Date | string) =>
    new Date(value).toLocaleDateString("en-CA", { timeZone: "Asia/Ho_Chi_Minh" });

  /**
   * Returns all journals that are due for review (next_review_date <= today)
   * plus all journals that have never been reviewed.
   * Sorted by journal createdAt ASC (oldest first).
   */
  const getDueJournals: JournalReviewController["getDueJournals"] = async (request, response) => {
    const userId = request.user?.id;

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      // Fetch all journals and all reviews for the user
      const allJournals = await journalRepo.find({
        where: { userId },
        order: { createdAt: "ASC" }
      });

      const allReviews = await reviewRepo.find({ where: { userId } });
      const reviewByJournalId = new Map<number, JournalReviewEntity>(
        allReviews.map((review) => [review.journalId, review])
      );

      const todayKey = toDateKey(new Date());
      const dueItems: Array<{
        journalId: number;
        summary: string;
        createdAt: string;
        review: ReturnType<typeof serialiseReview> | null;
      }> = [];

      for (const journal of allJournals) {
        const review = reviewByJournalId.get(journal.id);

        if (!review) {
          // Never reviewed → include as new
          dueItems.push({
            journalId: journal.id,
            summary: journal.summary,
            createdAt: journal.createdAt.toISOString(),
            review: null
          });
          continue;
        }

        // Already reviewed → include only if due today or earlier
        const reviewDateKey = toDateKey(review.nextReviewDate);
        if (reviewDateKey <= todayKey) {
          dueItems.push({
            journalId: journal.id,
            summary: journal.summary,
            createdAt: journal.createdAt.toISOString(),
            review: serialiseReview(review)
          });
        }
      }

      response.json({ journals: dueItems, total: dueItems.length });
    } catch (error) {
      console.error("Failed to get due journals for review.", error);
      response.status(500).json({ message: "Failed to get due journals for review" });
    }
  };

  /**
   * Submits a journal review rating (1–4) and schedules the next review via FSRS.
   * Creates the review row on first rating, updates on subsequent ratings.
   */
  const reviewJournal: JournalReviewController["reviewJournal"] = async (request, response) => {
    const userId = request.user?.id;

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const { rating, journalId } = request.body as {
      rating?: number;
      journalId?: number;
    };

    if (!rating || rating < 1 || rating > 4) {
      response.status(400).json({ message: "Rating must be 1–4" });
      return;
    }

    if (!journalId) {
      response.status(400).json({ message: "journalId is required" });
      return;
    }

    try {
      // Verify journal belongs to user
      const journal = await journalRepo.findOne({ where: { id: journalId, userId } });

      if (!journal) {
        response.status(404).json({ message: "Journal not found" });
        return;
      }

      const existingReview = await reviewRepo.findOne({
        where: { journalId, userId }
      });

      const currentState = existingReview
        ? {
            stability: existingReview.stability,
            difficulty: existingReview.difficulty,
            lapses: existingReview.lapses,
            currentIntervalDays: existingReview.currentIntervalDays,
            nextReviewDate:
              existingReview.nextReviewDate instanceof Date
                ? existingReview.nextReviewDate.toISOString()
                : String(existingReview.nextReviewDate),
            lastReviewDate: existingReview.lastReviewDate
              ? existingReview.lastReviewDate instanceof Date
                ? existingReview.lastReviewDate.toISOString()
                : String(existingReview.lastReviewDate)
              : null,
            reviewHistory: (() => {
              try {
                return JSON.parse(existingReview.reviewHistoryJson || "[]") as ReviewHistoryEntry[];
              } catch {
                return [];
              }
            })()
          }
        : createInitialReviewState();

      const updated = updateReviewAfterRating(currentState, rating as FSRSRating);

      const nextReview = {
        journalId,
        userId,
        stability: updated.stability,
        difficulty: updated.difficulty,
        lapses: updated.lapses,
        currentIntervalDays: updated.currentIntervalDays,
        nextReviewDate: new Date(updated.nextReviewDate),
        lastReviewDate: updated.lastReviewDate ? new Date(updated.lastReviewDate) : null,
        reviewHistoryJson: JSON.stringify(updated.reviewHistory)
      };

      const saved = await reviewRepo.save(
        existingReview ? { ...existingReview, ...nextReview } : reviewRepo.create(nextReview)
      );

      response.json({
        journal: {
          journalId: journal.id,
          summary: journal.summary,
          createdAt: journal.createdAt.toISOString()
        },
        review: serialiseReview(saved)
      });
    } catch (error) {
      console.error("Failed to review journal.", error);
      response.status(500).json({ message: "Failed to review journal" });
    }
  };

  return {
    getDueJournals,
    reviewJournal
  };
};
