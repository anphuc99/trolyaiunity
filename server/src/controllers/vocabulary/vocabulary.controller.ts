import type { Request, Response } from "express";
import type { DataSource, Repository } from "typeorm";
import VocabularyEntity from "../../models/vocabulary.entity.js";
import VocabularyReviewEntity from "../../models/vocabulary-review.entity.js";
import VocabularyMemoryEntity from "../../models/vocabulary-memory.entity.js";
import {
  createInitialReviewState,
  createReviewFromDifficulty,
  updateReviewAfterRating,
  type FSRSRating,
  type ReviewHistoryEntry
} from "../../services/fsrs.service.js";

interface VocabularyController {
  listVocabularies: (request: Request, response: Response) => Promise<void>;
  getVocabulary: (request: Request, response: Response) => Promise<void>;
  collectVocabulary: (request: Request, response: Response) => Promise<void>;
  updateVocabulary: (request: Request, response: Response) => Promise<void>;
  deleteVocabulary: (request: Request, response: Response) => Promise<void>;
  reviewVocabulary: (request: Request, response: Response) => Promise<void>;
  getDueReviews: (request: Request, response: Response) => Promise<void>;
  getStats: (request: Request, response: Response) => Promise<void>;
  saveMemory: (request: Request, response: Response) => Promise<void>;
  toggleStar: (request: Request, response: Response) => Promise<void>;
  setCardDirection: (request: Request, response: Response) => Promise<void>;
}

/**
 * Serialises a review entity to a client-facing JSON shape.
 */
const serialiseReview = (entity: VocabularyReviewEntity) => {
  let reviewHistory: ReviewHistoryEntry[] = [];

  try {
    reviewHistory = JSON.parse(entity.reviewHistoryJson || "[]") as ReviewHistoryEntry[];
  } catch {
    reviewHistory = [];
  }

  return {
    id: entity.id,
    vocabularyId: entity.vocabularyId,
    stability: entity.stability,
    difficulty: entity.difficulty,
    lapses: entity.lapses,
    currentIntervalDays: entity.currentIntervalDays,
    nextReviewDate: entity.nextReviewDate,
    lastReviewDate: entity.lastReviewDate,
    cardDirection: entity.cardDirection,
    isStarred: entity.isStarred,
    reviewHistory
  };
};

/**
 * Serialises a memory entity to a client-facing JSON shape.
 */
const serialiseMemory = (entity: VocabularyMemoryEntity) => {
  let linkedMessageIds: string[] = [];

  try {
    linkedMessageIds = JSON.parse(entity.linkedMessageIdsJson || "[]") as string[];
  } catch {
    linkedMessageIds = [];
  }

  return {
    id: entity.id,
    vocabularyId: entity.vocabularyId,
    userMemory: entity.userMemory,
    linkedMessageIds,
    createdAt: entity.createdAt,
    updatedAt: entity.updatedAt
  };
};

/**
 * Validates if a string looks like a valid UUID or vocabulary ID.
 */
const isValidVocabId = (id: string | undefined): boolean => {
  if (!id) return false;
  return id.trim().length > 0;
};

/**
 * Builds the vocabulary controller.
 *
 * @param dataSource - Initialised TypeORM data source.
 * @returns Vocabulary controller handlers.
 */
export const createVocabularyController = (dataSource: DataSource): VocabularyController => {
  const vocabRepo: Repository<VocabularyEntity> = dataSource.getRepository(VocabularyEntity);
  const reviewRepo: Repository<VocabularyReviewEntity> = dataSource.getRepository(VocabularyReviewEntity);
  const memoryRepo: Repository<VocabularyMemoryEntity> = dataSource.getRepository(VocabularyMemoryEntity);
  const toDateKey = (value: Date | string) =>
    new Date(value).toLocaleDateString("en-CA", { timeZone: "Asia/Ho_Chi_Minh" });

  // ──────────────────────────────────────────────────────────────────────────
  // List all vocabularies for the current user (with reviews + memories).
  // ──────────────────────────────────────────────────────────────────────────
  const listVocabularies: VocabularyController["listVocabularies"] = async (request, response) => {
    const userId = request.user?.id;

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const vocabularies = await vocabRepo.find({
        where: { userId },
        order: { createdAt: "DESC" }
      });

      const reviews = await reviewRepo.find({ where: { userId } });
      const memories = await memoryRepo.find({ where: { userId } });

      const reviewMap = new Map<string, VocabularyReviewEntity>(reviews.map((r: VocabularyReviewEntity) => [r.vocabularyId, r]));
      const memoryMap = new Map<string, VocabularyMemoryEntity>(memories.map((m: VocabularyMemoryEntity) => [m.vocabularyId, m]));

      const items = vocabularies.map((vocab: VocabularyEntity) => {
        const review = reviewMap.get(vocab.id);
        const memory = memoryMap.get(vocab.id);

        return {
          ...vocab,
          review: review ? serialiseReview(review) : null,
          memory: memory ? serialiseMemory(memory) : null
        };
      });

      response.json({ vocabularies: items });
    } catch (error) {
      console.error("Failed to list vocabularies.", error);
      response.status(500).json({ message: "Failed to list vocabularies" });
    }
  };

  // ──────────────────────────────────────────────────────────────────────────
  // Get a single vocabulary with review + memory.
  // ──────────────────────────────────────────────────────────────────────────
  const getVocabulary: VocabularyController["getVocabulary"] = async (request, response) => {
    const userId = request.user?.id;
    const vocabId = String(request.params.id);

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    if (!isValidVocabId(vocabId)) {
      response.status(400).json({ message: "Invalid vocabulary ID" });
      return;
    }

    try {
      const vocab = await vocabRepo.findOne({ where: { id: vocabId, userId } });

      if (!vocab) {
        response.status(404).json({ message: "Vocabulary not found" });
        return;
      }

      const review = await reviewRepo.findOne({ where: { vocabularyId: vocabId, userId } });
      const memory = await memoryRepo.findOne({ where: { vocabularyId: vocabId, userId } });

      response.json({
        ...vocab,
        review: review ? serialiseReview(review) : null,
        memory: memory ? serialiseMemory(memory) : null
      });
    } catch (error) {
      console.error("Failed to get vocabulary.", error);
      response.status(500).json({ message: "Failed to get vocabulary" });
    }
  };

  // ──────────────────────────────────────────────────────────────────────────
  // Collect (create) a new vocabulary — optionally with initial memory and
  // an initial difficulty rating that seeds the FSRS review.
  // ──────────────────────────────────────────────────────────────────────────
  const collectVocabulary: VocabularyController["collectVocabulary"] = async (request, response) => {
    const userId = request.user?.id;

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const {
      korean,
      vietnamese,
      memory,
      linkedMessageIds,
      difficultyRating
    } = request.body as {
      korean?: string;
      vietnamese?: string;
      memory?: string;
      linkedMessageIds?: string[];
      difficultyRating?: "very_easy" | "easy" | "medium" | "hard";
    };

    const trimmedKorean = (korean ?? "").trim();
    const trimmedVietnamese = (vietnamese ?? "").trim();

    if (!trimmedKorean || !trimmedVietnamese) {
      response.status(400).json({ message: "Korean and Vietnamese are required" });
      return;
    }

    try {
      // Check duplicate
      const existing = await vocabRepo.findOne({ where: { korean: trimmedKorean, userId } });

      if (existing) {
        response.status(409).json({ message: "Vocabulary already exists", vocabulary: existing });
        return;
      }

      // Create vocabulary
      const vocab = vocabRepo.create({
        korean: trimmedKorean,
        vietnamese: trimmedVietnamese,
        isManuallyAdded: !memory,
        userId
      });

      const saved = await vocabRepo.save(vocab);

      // Create review
      const reviewState = difficultyRating
        ? createReviewFromDifficulty(difficultyRating)
        : createInitialReviewState();

      const reviewEntity = reviewRepo.create({
        vocabularyId: saved.id,
        userId,
        stability: reviewState.stability,
        difficulty: reviewState.difficulty,
        lapses: reviewState.lapses,
        currentIntervalDays: reviewState.currentIntervalDays,
        nextReviewDate: new Date(reviewState.nextReviewDate),
        lastReviewDate: reviewState.lastReviewDate ? new Date(reviewState.lastReviewDate) : null,
        reviewHistoryJson: JSON.stringify(reviewState.reviewHistory)
      });

      const savedReview = await reviewRepo.save(reviewEntity);

      // Optionally create memory
      let savedMemory: VocabularyMemoryEntity | null = null;

      if (memory && memory.trim()) {
        const memoryEntity = memoryRepo.create({
          vocabularyId: saved.id,
          userId,
          userMemory: memory.trim(),
          linkedMessageIdsJson: JSON.stringify(linkedMessageIds ?? [])
        });

        savedMemory = await memoryRepo.save(memoryEntity);
      }

      response.status(201).json({
        ...saved,
        review: serialiseReview(savedReview),
        memory: savedMemory ? serialiseMemory(savedMemory) : null
      });
    } catch (error) {
      console.error("Failed to collect vocabulary.", error);
      response.status(500).json({ message: "Failed to collect vocabulary" });
    }
  };

  // ──────────────────────────────────────────────────────────────────────────
  // Update vocabulary Korean/Vietnamese text.
  // ──────────────────────────────────────────────────────────────────────────
  const updateVocabulary: VocabularyController["updateVocabulary"] = async (request, response) => {
    const userId = request.user?.id;
    const vocabId = String(request.params.id);

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    if (!isValidVocabId(vocabId)) {
      response.status(400).json({ message: "Invalid vocabulary ID" });
      return;
    }

    const { korean, vietnamese } = request.body as { korean?: string; vietnamese?: string };

    try {
      const vocab = await vocabRepo.findOne({ where: { id: vocabId, userId } });

      if (!vocab) {
        response.status(404).json({ message: "Vocabulary not found" });
        return;
      }

      if (korean?.trim()) {
        vocab.korean = korean.trim();
      }

      if (vietnamese?.trim()) {
        vocab.vietnamese = vietnamese.trim();
      }

      const updated = await vocabRepo.save(vocab);
      response.json(updated);
    } catch (error) {
      console.error("Failed to update vocabulary.", error);
      response.status(500).json({ message: "Failed to update vocabulary" });
    }
  };

  // ──────────────────────────────────────────────────────────────────────────
  // Delete vocabulary (cascade removes review + memory).
  // ──────────────────────────────────────────────────────────────────────────
  const deleteVocabulary: VocabularyController["deleteVocabulary"] = async (request, response) => {
    const userId = request.user?.id;
    const vocabId = String(request.params.id);

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    if (!isValidVocabId(vocabId)) {
      response.status(400).json({ message: "Invalid vocabulary ID" });
      return;
    }

    try {
      const vocab = await vocabRepo.findOne({ where: { id: vocabId, userId } });

      if (!vocab) {
        response.status(404).json({ message: "Vocabulary not found" });
        return;
      }

      await vocabRepo.remove(vocab);
      response.json({ message: "Vocabulary deleted" });
    } catch (error) {
      console.error("Failed to delete vocabulary.", error);
      response.status(500).json({ message: "Failed to delete vocabulary" });
    }
  };

  // ──────────────────────────────────────────────────────────────────────────
  // Submit a review rating (FSRS update).
  // ──────────────────────────────────────────────────────────────────────────
  const reviewVocabulary: VocabularyController["reviewVocabulary"] = async (request, response) => {
    const userId = request.user?.id;
    const vocabId = String(request.params.id);

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    if (!isValidVocabId(vocabId)) {
      response.status(400).json({ message: "Invalid vocabulary ID" });
      return;
    }

    const { rating } = request.body as { rating?: number };

    if (!rating || rating < 1 || rating > 4) {
      response.status(400).json({ message: "Rating must be 1–4" });
      return;
    }

    try {
      const reviewEntity = await reviewRepo.findOne({ where: { vocabularyId: vocabId, userId } });

      if (!reviewEntity) {
        response.status(404).json({ message: "Review not found" });
        return;
      }

      let history: ReviewHistoryEntry[] = [];
      try {
        history = JSON.parse(reviewEntity.reviewHistoryJson || "[]") as ReviewHistoryEntry[];
      } catch {
        history = [];
      }

      const currentState = {
        stability: reviewEntity.stability,
        difficulty: reviewEntity.difficulty,
        lapses: reviewEntity.lapses,
        currentIntervalDays: reviewEntity.currentIntervalDays,
        nextReviewDate: reviewEntity.nextReviewDate instanceof Date
          ? reviewEntity.nextReviewDate.toISOString()
          : String(reviewEntity.nextReviewDate),
        lastReviewDate: reviewEntity.lastReviewDate
          ? reviewEntity.lastReviewDate instanceof Date
            ? reviewEntity.lastReviewDate.toISOString()
            : String(reviewEntity.lastReviewDate)
          : null,
        reviewHistory: history
      };

      const updated = updateReviewAfterRating(currentState, rating as FSRSRating);

      reviewEntity.stability = updated.stability;
      reviewEntity.difficulty = updated.difficulty;
      reviewEntity.lapses = updated.lapses;
      reviewEntity.currentIntervalDays = updated.currentIntervalDays;
      reviewEntity.nextReviewDate = new Date(updated.nextReviewDate);
      reviewEntity.lastReviewDate = updated.lastReviewDate ? new Date(updated.lastReviewDate) : null;
      reviewEntity.reviewHistoryJson = JSON.stringify(updated.reviewHistory);

      const saved = await reviewRepo.save(reviewEntity);
      response.json(serialiseReview(saved));
    } catch (error) {
      console.error("Failed to review vocabulary.", error);
      response.status(500).json({ message: "Failed to review vocabulary" });
    }
  };

  // ──────────────────────────────────────────────────────────────────────────
  // Get vocabularies due for review today.
  // ──────────────────────────────────────────────────────────────────────────
  const getDueReviews: VocabularyController["getDueReviews"] = async (request, response) => {
    const userId = request.user?.id;

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const reviews = await reviewRepo.find({ where: { userId } });
      const todayKey = toDateKey(new Date());
      const dueReviews = reviews.filter((r: VocabularyReviewEntity) => toDateKey(r.nextReviewDate) <= todayKey);

      const vocabIds = dueReviews.map((r: VocabularyReviewEntity) => r.vocabularyId);
      const vocabularies = vocabIds.length
        ? await vocabRepo
            .createQueryBuilder("v")
            .where("v.id IN (:...ids)", { ids: vocabIds })
            .andWhere("v.user_id = :userId", { userId })
            .getMany()
        : [];

      const memories = vocabIds.length
        ? await memoryRepo
            .createQueryBuilder("m")
            .where("m.vocabulary_id IN (:...ids)", { ids: vocabIds })
            .andWhere("m.user_id = :userId", { userId })
            .getMany()
        : [];

      const vocabMap = new Map<string, VocabularyEntity>(vocabularies.map((v: VocabularyEntity) => [v.id, v]));
      const memoryMap = new Map<string, VocabularyMemoryEntity>(memories.map((m: VocabularyMemoryEntity) => [m.vocabularyId, m]));

      const items = dueReviews
        .map((r: VocabularyReviewEntity) => {
          const vocab = vocabMap.get(r.vocabularyId);
          if (!vocab) {
            return null;
          }

          const memory = memoryMap.get(r.vocabularyId);
          return {
            ...vocab,
            review: serialiseReview(r),
            memory: memory ? serialiseMemory(memory) : null
          };
        })
        .filter(Boolean);

      response.json({ vocabularies: items, total: items.length });
    } catch (error) {
      console.error("Failed to get due reviews.", error);
      response.status(500).json({ message: "Failed to get due reviews" });
    }
  };

  // ──────────────────────────────────────────────────────────────────────────
  // Get vocabulary stats (total, due, new, starred, etc.).
  // ──────────────────────────────────────────────────────────────────────────
  const getStats: VocabularyController["getStats"] = async (request, response) => {
    const userId = request.user?.id;

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const totalVocabularies = await vocabRepo.count({ where: { userId } });
      const totalReviews = await reviewRepo.count({ where: { userId } });
      const starredCount = await reviewRepo.count({ where: { userId, isStarred: true } });

      const allReviews = await reviewRepo.find({ where: { userId } });
      const now = new Date();
      const todayKey = toDateKey(now);
      const dueToday = allReviews.filter((r: VocabularyReviewEntity) => toDateKey(r.nextReviewDate) <= todayKey).length;
      const withoutReview = totalVocabularies - totalReviews;

      // Count difficult today (rated Hard/Again today)
      const todayStr = now.toLocaleDateString("en-CA", { timeZone: "Asia/Ho_Chi_Minh" });
      let difficultCount = 0;

      for (const review of allReviews) {
        let history: ReviewHistoryEntry[] = [];
        try {
          history = JSON.parse(review.reviewHistoryJson || "[]") as ReviewHistoryEntry[];
        } catch {
          continue;
        }

        const hasTodayDifficult = history.some((h) => {
          const reviewDate = new Date(h.date).toLocaleDateString("en-CA", { timeZone: "Asia/Ho_Chi_Minh" });
          return reviewDate === todayStr && (h.rating === 1 || h.rating === 2);
        });

        if (hasTodayDifficult) {
          difficultCount++;
        }
      }

      response.json({
        totalVocabularies,
        withReview: totalReviews,
        withoutReview,
        dueToday,
        starredCount,
        difficultCount
      });
    } catch (error) {
      console.error("Failed to get vocabulary stats.", error);
      response.status(500).json({ message: "Failed to get vocabulary stats" });
    }
  };

  // ──────────────────────────────────────────────────────────────────────────
  // Save / update a memory for a vocabulary.
  // ──────────────────────────────────────────────────────────────────────────
  const saveMemory: VocabularyController["saveMemory"] = async (request, response) => {
    const userId = request.user?.id;
    const vocabId = String(request.params.id);

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    if (!isValidVocabId(vocabId)) {
      response.status(400).json({ message: "Invalid vocabulary ID" });
      return;
    }

    const { userMemory, linkedMessageIds } = request.body as {
      userMemory?: string;
      linkedMessageIds?: string[];
    };

    if (!userMemory?.trim()) {
      response.status(400).json({ message: "Memory content is required" });
      return;
    }

    try {
      const vocab = await vocabRepo.findOne({ where: { id: vocabId, userId } });

      if (!vocab) {
        response.status(404).json({ message: "Vocabulary not found" });
        return;
      }

      let memoryEntity = await memoryRepo.findOne({ where: { vocabularyId: vocabId, userId } });

      if (memoryEntity) {
        memoryEntity.userMemory = userMemory.trim();
        memoryEntity.linkedMessageIdsJson = JSON.stringify(linkedMessageIds ?? []);
      } else {
        memoryEntity = memoryRepo.create({
          vocabularyId: vocabId,
          userId,
          userMemory: userMemory.trim(),
          linkedMessageIdsJson: JSON.stringify(linkedMessageIds ?? [])
        });
      }

      const saved = await memoryRepo.save(memoryEntity);
      response.json(serialiseMemory(saved));
    } catch (error) {
      console.error("Failed to save memory.", error);
      response.status(500).json({ message: "Failed to save memory" });
    }
  };

  // ──────────────────────────────────────────────────────────────────────────
  // Toggle star on a vocabulary review.
  // ──────────────────────────────────────────────────────────────────────────
  const toggleStar: VocabularyController["toggleStar"] = async (request, response) => {
    const userId = request.user?.id;
    const vocabId = String(request.params.id);

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    if (!isValidVocabId(vocabId)) {
      response.status(400).json({ message: "Invalid vocabulary ID" });
      return;
    }

    try {
      let reviewEntity = await reviewRepo.findOne({ where: { vocabularyId: vocabId, userId } });

      if (!reviewEntity) {
        // Create a default review if missing, then star it
        const state = createInitialReviewState();
        reviewEntity = reviewRepo.create({
          vocabularyId: vocabId,
          userId,
          stability: state.stability,
          difficulty: state.difficulty,
          lapses: state.lapses,
          currentIntervalDays: state.currentIntervalDays,
          nextReviewDate: new Date(state.nextReviewDate),
          lastReviewDate: null,
          reviewHistoryJson: "[]",
          isStarred: true
        });
      } else {
        reviewEntity.isStarred = !reviewEntity.isStarred;
      }

      const saved = await reviewRepo.save(reviewEntity);
      response.json(serialiseReview(saved));
    } catch (error) {
      console.error("Failed to toggle star.", error);
      response.status(500).json({ message: "Failed to toggle star" });
    }
  };

  // ──────────────────────────────────────────────────────────────────────────
  // Set card direction preference (kr-vn / vn-kr).
  // ──────────────────────────────────────────────────────────────────────────
  const setCardDirection: VocabularyController["setCardDirection"] = async (request, response) => {
    const userId = request.user?.id;
    const vocabId = String(request.params.id);

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    if (!isValidVocabId(vocabId)) {
      response.status(400).json({ message: "Invalid vocabulary ID" });
      return;
    }

    const { direction } = request.body as { direction?: string };
    const allowed = ["kr-vn", "vn-kr"];

    if (!direction || !allowed.includes(direction)) {
      response.status(400).json({ message: "Direction must be 'kr-vn' or 'vn-kr'" });
      return;
    }

    try {
      const reviewEntity = await reviewRepo.findOne({ where: { vocabularyId: vocabId, userId } });

      if (!reviewEntity) {
        response.status(404).json({ message: "Review not found" });
        return;
      }

      reviewEntity.cardDirection = direction;
      const saved = await reviewRepo.save(reviewEntity);
      response.json(serialiseReview(saved));
    } catch (error) {
      console.error("Failed to set card direction.", error);
      response.status(500).json({ message: "Failed to set card direction" });
    }
  };

  return {
    listVocabularies,
    getVocabulary,
    collectVocabulary,
    updateVocabulary,
    deleteVocabulary,
    reviewVocabulary,
    getDueReviews,
    getStats,
    saveMemory,
    toggleStar,
    setCardDirection
  };
};
