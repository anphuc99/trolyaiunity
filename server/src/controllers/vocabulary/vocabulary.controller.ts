import type { Request, Response } from "express";
import { IsNull, Not, type DataSource, type Repository } from "typeorm";
import VocabularyEntity from "../../models/vocabulary.entity.js";
import UserEntity from "../../models/user.entity.js";
import {
  createInitialReviewState,
  createReviewFromDifficulty,
  updateReviewAfterRating,
  type FSRSRating,
  type ReviewHistoryEntry
} from "../../services/fsrs.service.js";
import { createCheapAIService, type CheapAIService } from "../../services/cheap-ai.service.js";

interface VocabularyController {
  listVocabularies: (request: Request, response: Response) => Promise<void>;
  getVocabulary: (request: Request, response: Response) => Promise<void>;
  collectVocabulary: (request: Request, response: Response) => Promise<void>;
  updateVocabulary: (request: Request, response: Response) => Promise<void>;
  deleteVocabulary: (request: Request, response: Response) => Promise<void>;
  reviewVocabulary: (request: Request, response: Response) => Promise<void>;
  getLearnedCount: (request: Request, response: Response) => Promise<void>;
  getDueReviews: (request: Request, response: Response) => Promise<void>;
  getStats: (request: Request, response: Response) => Promise<void>;
  toggleStar: (request: Request, response: Response) => Promise<void>;
  setCardDirection: (request: Request, response: Response) => Promise<void>;
  lookupWord: (request: Request, response: Response) => Promise<void>;
  ignoreVocabulary: (request: Request, response: Response) => Promise<void>;
}

/**
 * Serialises a vocabulary entity (including merged FSRS fields) to a
 * client-facing JSON shape. The `reviewHistoryJson` text column is parsed
 * into an array before sending.
 */
const serialiseVocabulary = (entity: VocabularyEntity) => {
  let reviewHistory: ReviewHistoryEntry[] = [];

  try {
    reviewHistory = JSON.parse(entity.reviewHistoryJson || "[]") as ReviewHistoryEntry[];
  } catch {
    reviewHistory = [];
  }

  return {
    id: entity.id,
    korean: entity.korean,
    vietnamese: entity.vietnamese,
    pinyin: entity.pinyin ?? null,
    level: entity.level ?? null,
    isManuallyAdded: entity.isManuallyAdded,
    isIgnored: entity.isIgnored,
    userId: entity.userId,
    createdAt: entity.createdAt,
    updatedAt: entity.updatedAt,
    stability: entity.stability ?? null,
    difficulty: entity.difficulty ?? null,
    lapses: entity.lapses ?? null,
    currentIntervalDays: entity.currentIntervalDays ?? null,
    nextReviewDate: entity.nextReviewDate ?? null,
    lastReviewDate: entity.lastReviewDate ?? null,
    cardDirection: entity.cardDirection ?? "kr-vn",
    isStarred: entity.isStarred ?? false,
    reviewHistory
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
 * Normalizes vocabulary text before lookup/save.
 */
const normalizeVocabularyWord = (value: string): string => value.trim();

/**
 * Detects common SQL unique-constraint violations across MySQL/SQLite drivers.
 */
const isUniqueConstraintError = (error: unknown): boolean => {
  if (!error || typeof error !== "object") {
    return false;
  }

  const dbError = error as {
    code?: string;
    errno?: number;
    message?: string;
    sqlMessage?: string;
  };

  const message = `${dbError.message ?? ""} ${dbError.sqlMessage ?? ""}`.toLowerCase();

  return dbError.code === "ER_DUP_ENTRY"
    || dbError.errno === 1062
    || message.includes("duplicate entry")
    || message.includes("unique constraint failed");
};

/**
 * Checks whether a translation contains Han characters.
 */
const hasHanCharacters = (text: string): boolean => /[\u3400-\u9FFF]/u.test(text);

/**
 * Validates if a Vietnamese meaning is usable for display/storage.
 * Rejects empty values, same-as-source values, and Han-script outputs.
 */
const isValidVietnameseMeaning = (sourceWord: string, meaning: string): boolean => {
  const normalizedMeaning = meaning.trim();
  const normalizedSource = sourceWord.trim();

  if (!normalizedMeaning) {
    return false;
  }

  if (normalizedMeaning === normalizedSource) {
    return false;
  }

  if (hasHanCharacters(normalizedMeaning)) {
    return false;
  }

  return true;
};

/**
 * Builds the vocabulary controller.
 *
 * @param dataSource - Initialised TypeORM data source.
 * @returns Vocabulary controller handlers.
 */
export const createVocabularyController = (dataSource: DataSource): VocabularyController => {
  const vocabRepo: Repository<VocabularyEntity> = dataSource.getRepository(VocabularyEntity);
  const userRepo: Repository<UserEntity> = dataSource.getRepository(UserEntity);
  const toDateKey = (value: Date | string) =>
    new Date(value).toLocaleDateString("en-CA", { timeZone: "Asia/Ho_Chi_Minh" });

  /**
   * Resolves the current level label of the user (for example: HSK1, HSK2).
   */
  const resolveCurrentUserLevel = async (userId: number): Promise<string | null> => {
    const user = await userRepo.findOne({
      where: { id: userId },
      relations: { level: true }
    });

    return user?.level?.level?.trim() || null;
  };

  // ──────────────────────────────────────────────────────────────────────────
  // List all vocabularies for the current user.
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

      response.json({ vocabularies: vocabularies.map(serialiseVocabulary) });
    } catch (error) {
      console.error("Failed to list vocabularies.", error);
      response.status(500).json({ message: "Failed to list vocabularies" });
    }
  };

  // ──────────────────────────────────────────────────────────────────────────
  // Get a single vocabulary entry.
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

      response.json(serialiseVocabulary(vocab));
    } catch (error) {
      console.error("Failed to get vocabulary.", error);
      response.status(500).json({ message: "Failed to get vocabulary" });
    }
  };

  // ──────────────────────────────────────────────────────────────────────────
  // Collect (create) a new vocabulary with an optional initial difficulty
  // rating that seeds the FSRS review state.
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
      pinyin,
      level,
      difficultyRating
    } = request.body as {
      korean?: string;
      vietnamese?: string;
      pinyin?: string;
      level?: string;
      difficultyRating?: "very_easy" | "easy" | "medium" | "hard";
    };

    const trimmedKorean = normalizeVocabularyWord(korean ?? "");
    const trimmedVietnamese = (vietnamese ?? "").trim();
    const trimmedPinyin = (pinyin ?? "").trim();
    const trimmedLevel = (level ?? "").trim();

    if (!trimmedKorean || !trimmedVietnamese) {
      response.status(400).json({ message: "Korean and Vietnamese are required" });
      return;
    }

    if (!isValidVietnameseMeaning(trimmedKorean, trimmedVietnamese)) {
      response.status(400).json({ message: "Vietnamese meaning is invalid (cannot be Chinese or same as source word)" });
      return;
    }

    try {
      // Check duplicate
      const existing = await vocabRepo.findOne({ where: { korean: trimmedKorean, userId } });

      if (existing) {
        response.status(409).json({ message: "Vocabulary already exists", vocabulary: existing });
        return;
      }

      const currentUserLevel = await resolveCurrentUserLevel(userId);

      const vocab = vocabRepo.create({
        korean: trimmedKorean,
        vietnamese: trimmedVietnamese,
        pinyin: trimmedPinyin || null,
        level: trimmedLevel || currentUserLevel,
        isManuallyAdded: true,
        isIgnored: false,
        cardDirection: "kr-vn",
        isStarred: false,
        userId
      });

      // Seed FSRS review state immediately
      const reviewState = difficultyRating
        ? createReviewFromDifficulty(difficultyRating)
        : createInitialReviewState();

      vocab.stability = reviewState.stability;
      vocab.difficulty = reviewState.difficulty;
      vocab.lapses = reviewState.lapses;
      vocab.currentIntervalDays = reviewState.currentIntervalDays;
      vocab.nextReviewDate = new Date(reviewState.nextReviewDate);
      vocab.lastReviewDate = reviewState.lastReviewDate ? new Date(reviewState.lastReviewDate) : null;
      vocab.reviewHistoryJson = JSON.stringify(reviewState.reviewHistory);

      const saved = await vocabRepo.save(vocab);
      response.status(201).json(serialiseVocabulary(saved));
    } catch (error) {
      if (isUniqueConstraintError(error)) {
        const existing = await vocabRepo.findOne({ where: { korean: trimmedKorean, userId } });
        response.status(409).json({ message: "Vocabulary already exists", vocabulary: existing ?? null });
        return;
      }

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

    const { korean, vietnamese, pinyin, level } = request.body as { korean?: string; vietnamese?: string; pinyin?: string; level?: string };
    const normalizedKorean = typeof korean === "string" ? normalizeVocabularyWord(korean) : null;

    try {
      const vocab = await vocabRepo.findOne({ where: { id: vocabId, userId } });

      if (!vocab) {
        response.status(404).json({ message: "Vocabulary not found" });
        return;
      }

      if (typeof korean === "string") {
        if (!normalizedKorean) {
          response.status(400).json({ message: "Korean cannot be empty" });
          return;
        }

        if (normalizedKorean !== vocab.korean) {
          const duplicate = await vocabRepo.findOne({
            where: {
              korean: normalizedKorean,
              userId,
              id: Not(vocabId)
            }
          });

          if (duplicate) {
            response.status(409).json({ message: "Vocabulary already exists", vocabulary: duplicate });
            return;
          }
        }

        vocab.korean = normalizedKorean;
      }

      if (vietnamese?.trim()) {
        const normalizedMeaning = vietnamese.trim();
        const sourceWord = ((typeof korean === "string" ? normalizedKorean : vocab.korean) || "").trim();

        if (!isValidVietnameseMeaning(sourceWord, normalizedMeaning)) {
          response.status(400).json({ message: "Vietnamese meaning is invalid (cannot be Chinese or same as source word)" });
          return;
        }

        vocab.vietnamese = normalizedMeaning;
      }

      if (typeof pinyin === "string") {
        const trimmedPinyin = pinyin.trim();
        vocab.pinyin = trimmedPinyin || null;
      }

      if (typeof level === "string") {
        const trimmedLevel = level.trim();
        vocab.level = trimmedLevel || null;
      }

      const updated = await vocabRepo.save(vocab);
      response.json(serialiseVocabulary(updated));
    } catch (error) {
      if (isUniqueConstraintError(error)) {
        response.status(409).json({ message: "Vocabulary already exists" });
        return;
      }

      console.error("Failed to update vocabulary.", error);
      response.status(500).json({ message: "Failed to update vocabulary" });
    }
  };

  // ──────────────────────────────────────────────────────────────────────────
  // Delete vocabulary (cascade removes review).
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
      const vocab = await vocabRepo.findOne({ where: { id: vocabId, userId } });

      if (!vocab || !vocab.nextReviewDate) {
        response.status(404).json({ message: "Vocabulary not found or review not initialised" });
        return;
      }

      const todayKey = toDateKey(new Date());
      const nextReviewKey = toDateKey(vocab.nextReviewDate);

      if (nextReviewKey > todayKey) {
        // Not yet due — return current state without updating
        response.json(serialiseVocabulary(vocab));
        return;
      }

      let history: ReviewHistoryEntry[] = [];
      try {
        history = JSON.parse(vocab.reviewHistoryJson || "[]") as ReviewHistoryEntry[];
      } catch {
        history = [];
      }

      const currentState = {
        stability: vocab.stability ?? 0,
        difficulty: vocab.difficulty ?? 5,
        lapses: vocab.lapses ?? 0,
        currentIntervalDays: vocab.currentIntervalDays ?? 0,
        nextReviewDate: vocab.nextReviewDate instanceof Date
          ? vocab.nextReviewDate.toISOString()
          : String(vocab.nextReviewDate),
        lastReviewDate: vocab.lastReviewDate
          ? vocab.lastReviewDate instanceof Date
            ? vocab.lastReviewDate.toISOString()
            : String(vocab.lastReviewDate)
          : null,
        reviewHistory: history
      };

      const updated = updateReviewAfterRating(currentState, rating as FSRSRating);

      vocab.stability = updated.stability;
      vocab.difficulty = updated.difficulty;
      vocab.lapses = updated.lapses;
      vocab.currentIntervalDays = updated.currentIntervalDays;
      vocab.nextReviewDate = new Date(updated.nextReviewDate);
      vocab.lastReviewDate = updated.lastReviewDate ? new Date(updated.lastReviewDate) : null;
      vocab.reviewHistoryJson = JSON.stringify(updated.reviewHistory);

      const saved = await vocabRepo.save(vocab);
      response.json(serialiseVocabulary(saved));
    } catch (error) {
      console.error("Failed to review vocabulary.", error);
      response.status(500).json({ message: "Failed to review vocabulary" });
    }
  };

  // ──────────────────────────────────────────────────────────────────────────
  // Count vocabularies that were actually reviewed at least once.
  // ──────────────────────────────────────────────────────────────────────────
  const getLearnedCount: VocabularyController["getLearnedCount"] = async (request, response) => {
    const userId = request.user?.id;

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const count = await vocabRepo.count({
        where: {
          userId,
          lastReviewDate: Not(IsNull())
        }
      });

      response.json({ count });
    } catch (error) {
      console.error("Failed to get learned vocabulary count.", error);
      response.status(500).json({ message: "Failed to get learned vocabulary count" });
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
      // Fetch only vocabs that have been initialised for review
      const vocabs = await vocabRepo.find({
        where: { userId, nextReviewDate: Not(IsNull()) }
      });

      const todayKey = toDateKey(new Date());
      const dueVocabs = vocabs.filter(
        (v: VocabularyEntity) => toDateKey(v.nextReviewDate!) <= todayKey && !v.isIgnored
      );

      response.json({ vocabularies: dueVocabs.map(serialiseVocabulary), total: dueVocabs.length });
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
      const withReview = await vocabRepo.count({ where: { userId, nextReviewDate: Not(IsNull()) } });
      const starredCount = await vocabRepo.count({ where: { userId, isStarred: true } });

      const vocabsWithReview = await vocabRepo.find({
        where: { userId, nextReviewDate: Not(IsNull()) }
      });

      const now = new Date();
      const todayKey = toDateKey(now);
      const dueToday = vocabsWithReview.filter(
        (v: VocabularyEntity) => toDateKey(v.nextReviewDate!) <= todayKey
      ).length;
      const withoutReview = totalVocabularies - withReview;

      // Count difficult today (rated Hard/Again today)
      const todayStr = now.toLocaleDateString("en-CA", { timeZone: "Asia/Ho_Chi_Minh" });
      let difficultCount = 0;

      for (const vocab of vocabsWithReview) {
        let history: ReviewHistoryEntry[] = [];
        try {
          history = JSON.parse(vocab.reviewHistoryJson || "[]") as ReviewHistoryEntry[];
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
        withReview,
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
  // Toggle star on a vocabulary entry. Initialises FSRS state if needed.
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
      const vocab = await vocabRepo.findOne({ where: { id: vocabId, userId } });

      if (!vocab) {
        response.status(404).json({ message: "Vocabulary not found" });
        return;
      }

      if (!vocab.nextReviewDate) {
        // FSRS state not yet initialised — initialise and star in one step
        const state = createInitialReviewState();
        vocab.stability = state.stability;
        vocab.difficulty = state.difficulty;
        vocab.lapses = state.lapses;
        vocab.currentIntervalDays = state.currentIntervalDays;
        vocab.nextReviewDate = new Date(state.nextReviewDate);
        vocab.lastReviewDate = null;
        vocab.reviewHistoryJson = "[]";
        vocab.isStarred = true;
      } else {
        vocab.isStarred = !(vocab.isStarred ?? false);
      }

      const saved = await vocabRepo.save(vocab);
      response.json(serialiseVocabulary(saved));
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
      const vocab = await vocabRepo.findOne({ where: { id: vocabId, userId } });

      if (!vocab) {
        response.status(404).json({ message: "Vocabulary not found" });
        return;
      }

      vocab.cardDirection = direction;
      const saved = await vocabRepo.save(vocab);
      response.json(serialiseVocabulary(saved));
    } catch (error) {
      console.error("Failed to set card direction.", error);
      response.status(500).json({ message: "Failed to set card direction" });
    }
  };

  // ──────────────────────────────────────────────────────────────────────────
  // Ignore a vocabulary — marks it as ignored so it never appears in reviews.
  // ──────────────────────────────────────────────────────────────────────────
  const ignoreVocabulary: VocabularyController["ignoreVocabulary"] = async (request, response) => {
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

      vocab.isIgnored = true;
      const saved = await vocabRepo.save(vocab);
      response.json({ message: "Vocabulary ignored", id: saved.id });
    } catch (error) {
      console.error("Failed to ignore vocabulary.", error);
      response.status(500).json({ message: "Failed to ignore vocabulary" });
    }
  };

  // ──────────────────────────────────────────────────────────────────────────
  // Lookup a vocabulary by its Chinese word text.
  // Returns existing DB data or uses cheap AI to translate on the fly.
  // If the word is new, automatically creates the vocabulary entry with
  // an initialised FSRS review state.
  // ──────────────────────────────────────────────────────────────────────────
  const lookupWord: VocabularyController["lookupWord"] = async (request, response) => {
    const userId = request.user?.id;

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const word = normalizeVocabularyWord(String(request.query.word ?? ""));
    if (!word) {
      response.status(400).json({ message: "Query parameter 'word' is required" });
      return;
    }

    try {
      const cheapAI: CheapAIService = createCheapAIService();

      // Try to find existing vocabulary by korean (Chinese) text
      const existing = await vocabRepo.findOne({ where: { korean: word, userId } });

      if (existing) {
        let pinyin = existing.pinyin ?? "";
        let vietnamese = existing.vietnamese ?? "";
        const meaningNeedsRefresh = !isValidVietnameseMeaning(word, vietnamese);

        // If pinyin is missing or meaning is invalid, refresh via cheap AI.
        if (!pinyin || meaningNeedsRefresh) {
          try {
            const translation = await cheapAI.translateVocabulary(word);
            if (!pinyin && translation.pinyin) {
              pinyin = translation.pinyin;
              existing.pinyin = pinyin;
            }
            if (isValidVietnameseMeaning(word, translation.vietnamese ?? "")) {
              vietnamese = translation.vietnamese;
              existing.vietnamese = vietnamese;
            } else if (meaningNeedsRefresh) {
              vietnamese = "chua co nghia";
              existing.vietnamese = vietnamese;
            }
            await vocabRepo.save(existing);
          } catch (aiError) {
            console.warn("Cheap AI translation fallback failed:", aiError);
            if (meaningNeedsRefresh) {
              vietnamese = "chua co nghia";
            }
          }
        }

        response.json({
          ...serialiseVocabulary(existing),
          vietnamese,
          pinyin,
          isNew: false
        });
        return;
      }

      // Word not in DB — use cheap AI to translate, then auto-create
      let pinyin = "";
      let vietnamese = "";
      try {
        const translation = await cheapAI.translateVocabulary(word);
        pinyin = translation.pinyin;
        if (isValidVietnameseMeaning(word, translation.vietnamese ?? "")) {
          vietnamese = translation.vietnamese;
        }
      } catch (aiError) {
        console.warn("Cheap AI translation failed for new word:", aiError);
      }

      vietnamese = vietnamese || "chua co nghia";

      const currentUserLevel = await resolveCurrentUserLevel(userId);

      const vocab = vocabRepo.create({
        korean: word,
        vietnamese,
        pinyin: pinyin || null,
        level: currentUserLevel,
        isManuallyAdded: false,
        isIgnored: false,
        cardDirection: "kr-vn",
        isStarred: false,
        userId
      });

      // Initialise FSRS review state immediately
      const reviewState = createInitialReviewState();
      vocab.stability = reviewState.stability;
      vocab.difficulty = reviewState.difficulty;
      vocab.lapses = reviewState.lapses;
      vocab.currentIntervalDays = reviewState.currentIntervalDays;
      vocab.nextReviewDate = new Date(reviewState.nextReviewDate);
      vocab.lastReviewDate = null;
      vocab.reviewHistoryJson = JSON.stringify(reviewState.reviewHistory);

      const saved = await vocabRepo.save(vocab);

      response.json({
        ...serialiseVocabulary(saved),
        isNew: true
      });
    } catch (error) {
      if (isUniqueConstraintError(error)) {
        const existing = await vocabRepo.findOne({ where: { korean: word, userId } });
        response.status(409).json({ message: "Vocabulary already exists", vocabulary: existing ?? null });
        return;
      }

      console.error("Failed to lookup vocabulary word.", error);
      response.status(500).json({ message: "Failed to lookup vocabulary word" });
    }
  };

  return {
    listVocabularies,
    getVocabulary,
    collectVocabulary,
    updateVocabulary,
    deleteVocabulary,
    reviewVocabulary,
    getLearnedCount,
    getDueReviews,
    getStats,
    toggleStar,
    setCardDirection,
    lookupWord,
    ignoreVocabulary
  };
};
