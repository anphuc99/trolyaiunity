import { fsrs, createEmptyCard, Rating, State, type Card, type Grade, type FSRS } from "ts-fsrs";

/**
 * FSRS (Free Spaced Repetition Scheduler) service.
 *
 * Wraps the ts-fsrs library to provide scheduling for vocabulary reviews.
 * Mirrors the old mimichat logic: 4-level rating, stability, difficulty,
 * retrievability, and review history tracking.
 */

// ────────────────────────────────────────────────────────────────────────────
// Types
// ────────────────────────────────────────────────────────────────────────────

/** 1=Again, 2=Hard, 3=Good, 4=Easy */
export type FSRSRating = 1 | 2 | 3 | 4;

export interface FSRSSettings {
  maxReviewsPerDay: number;
  newCardsPerDay: number;
  desiredRetention: number;
}

export const DEFAULT_FSRS_SETTINGS: FSRSSettings = {
  maxReviewsPerDay: 50,
  newCardsPerDay: 20,
  desiredRetention: 0.9
};

export interface ReviewHistoryEntry {
  date: string;
  rating: FSRSRating;
  stabilityBefore: number;
  stabilityAfter: number;
  difficultyBefore: number;
  difficultyAfter: number;
  retrievability: number;
}

export interface ReviewState {
  stability: number;
  difficulty: number;
  lapses: number;
  currentIntervalDays: number;
  nextReviewDate: string;
  lastReviewDate: string | null;
  reviewHistory: ReviewHistoryEntry[];
}

// ────────────────────────────────────────────────────────────────────────────
// Internal helpers
// ────────────────────────────────────────────────────────────────────────────

const fsrsCache = new Map<number, FSRS>();

/**
 * Returns a cached FSRS scheduler for the given desired retention.
 *
 * @param desiredRetention - Target recall probability (0.5–0.97).
 * @returns A ts-fsrs FSRS scheduler.
 */
const getFsrsScheduler = (desiredRetention = 0.9): FSRS => {
  const normalized = Math.max(0.5, Math.min(0.97, desiredRetention));
  const key = Number(normalized.toFixed(3));

  if (!fsrsCache.has(key)) {
    fsrsCache.set(key, fsrs({ request_retention: normalized }));
  }

  return fsrsCache.get(key)!;
};

/**
 * Maps our 4-level rating to ts-fsrs Grade.
 */
const mapRating = (rating: FSRSRating): Grade => {
  switch (rating) {
    case 1:
      return Rating.Again;
    case 2:
      return Rating.Hard;
    case 3:
      return Rating.Good;
    case 4:
      return Rating.Easy;
    default:
      return Rating.Good;
  }
};

/**
 * Converts persisted review state into a ts-fsrs Card.
 */
const toCard = (state: ReviewState): Card => {
  const hasStability = state.stability > 0;
  const hasHistory = state.reviewHistory.length > 0;

  if (!hasStability) {
    const initialStability = 3;
    const initialDifficulty = 5;

    return {
      due: hasHistory ? new Date(state.nextReviewDate) : new Date(),
      stability: initialStability,
      difficulty: initialDifficulty,
      elapsed_days: state.lastReviewDate
        ? Math.max(0, (Date.now() - new Date(state.lastReviewDate).getTime()) / 86_400_000)
        : 0,
      scheduled_days: state.currentIntervalDays || 0,
      learning_steps: 0,
      reps: hasHistory ? state.reviewHistory.length : 0,
      lapses: state.lapses || 0,
      state: hasHistory ? State.Review : State.New,
      last_review: state.lastReviewDate ? new Date(state.lastReviewDate) : undefined
    };
  }

  return {
    due: new Date(state.nextReviewDate),
    stability: state.stability,
    difficulty: state.difficulty || 5,
    elapsed_days: state.lastReviewDate
      ? Math.max(0, (Date.now() - new Date(state.lastReviewDate).getTime()) / 86_400_000)
      : 0,
    scheduled_days: state.currentIntervalDays || 0,
    learning_steps: 0,
    reps: state.reviewHistory.length,
    lapses: state.lapses || 0,
    state: state.lapses && state.lapses > 0 ? State.Relearning : State.Review,
    last_review: state.lastReviewDate ? new Date(state.lastReviewDate) : undefined
  };
};

// ────────────────────────────────────────────────────────────────────────────
// Public API
// ────────────────────────────────────────────────────────────────────────────

/**
 * Calculates retrievability (probability of recall).
 *
 * @param stability - Current stability in days.
 * @param elapsedDays - Days since last review.
 * @returns Recall probability [0–1].
 */
export const calculateRetrievability = (stability: number, elapsedDays: number): number => {
  if (stability <= 0) {
    return 0;
  }

  const DECAY = -0.5;
  const FACTOR = Math.pow(0.9, 1 / DECAY) - 1;
  return Math.pow(1 + (FACTOR * elapsedDays) / stability, DECAY);
};

/**
 * Builds the initial review state for a newly collected vocabulary.
 * The first review is scheduled immediately to show in learn/review.
 *
 * @returns A fresh ReviewState.
 */
export const createInitialReviewState = (): ReviewState => {
  const now = new Date();

  return {
    stability: 0,
    difficulty: 5,
    lapses: 0,
    currentIntervalDays: 1,
    nextReviewDate: now.toISOString(),
    lastReviewDate: null,
    reviewHistory: []
  };
};

/**
 * Builds a review state from a user difficulty rating at collection time.
 *
 * Maps: very_easy → 14d, easy → 7d, medium → 3d, hard → 1d.
 *
 * @param difficultyRating - User self-assessment when collecting.
 * @returns A pre-seeded ReviewState.
 */
export const createReviewFromDifficulty = (
  difficultyRating: "very_easy" | "easy" | "medium" | "hard"
): ReviewState => {
  const now = new Date();

  const fsrsRating: FSRSRating =
    difficultyRating === "very_easy" ? 4 : difficultyRating === "easy" ? 3 : difficultyRating === "medium" ? 2 : 1;

  const intervalDays =
    difficultyRating === "very_easy" ? 14 : difficultyRating === "easy" ? 7 : difficultyRating === "medium" ? 3 : 1;

  const initialStability = intervalDays;
  const initialDifficulty =
    difficultyRating === "very_easy" ? 1 : difficultyRating === "easy" ? 3 : difficultyRating === "medium" ? 5 : 7;

  const nextReviewDate = new Date(now);

  const historyEntry: ReviewHistoryEntry = {
    date: now.toISOString(),
    rating: fsrsRating,
    stabilityBefore: 0,
    stabilityAfter: initialStability,
    difficultyBefore: 5,
    difficultyAfter: initialDifficulty,
    retrievability: 1
  };

  return {
    stability: initialStability,
    difficulty: initialDifficulty,
    lapses: 0,
    currentIntervalDays: intervalDays,
    nextReviewDate: nextReviewDate.toISOString(),
    lastReviewDate: now.toISOString(),
    reviewHistory: [historyEntry]
  };
};

/**
 * Updates a review state after the user rates a card.
 * Uses ts-fsrs for scheduling.
 *
 * @param state - Current ReviewState.
 * @param rating - User rating (1–4).
 * @param settings - FSRS settings.
 * @returns Updated ReviewState.
 */
export const updateReviewAfterRating = (
  state: ReviewState,
  rating: FSRSRating,
  settings: FSRSSettings = DEFAULT_FSRS_SETTINGS
): ReviewState => {
  const now = new Date();
  const scheduler = getFsrsScheduler(settings.desiredRetention);
  const card = toCard(state);
  const schedulingCards = scheduler.repeat(card, now);
  const grade = mapRating(rating);
  const result = schedulingCards[grade];
  const newCard = result.card;

  const lastReviewDate = state.lastReviewDate ? new Date(state.lastReviewDate) : now;
  const elapsedDays = Math.max(0, (now.getTime() - lastReviewDate.getTime()) / 86_400_000);
  const retrievability =
    state.stability > 0 ? calculateRetrievability(state.stability, elapsedDays) : 1;

  const historyEntry: ReviewHistoryEntry = {
    date: now.toISOString(),
    rating,
    stabilityBefore: state.stability || 0,
    stabilityAfter: newCard.stability,
    difficultyBefore: state.difficulty || 5,
    difficultyAfter: newCard.difficulty,
    retrievability
  };

  return {
    stability: newCard.stability,
    difficulty: newCard.difficulty,
    lapses: newCard.lapses,
    currentIntervalDays: newCard.scheduled_days,
    nextReviewDate: newCard.due.toISOString(),
    lastReviewDate: now.toISOString(),
    reviewHistory: [...state.reviewHistory, historyEntry]
  };
};

/**
 * Calculates interval for a brand-new card given a rating.
 * Useful for preview ("If you rate Good, next review in X days").
 */
export const calculateNewCardInterval = (rating: FSRSRating, desiredRetention = 0.9): number => {
  const newCard = createEmptyCard();
  const scheduler = getFsrsScheduler(desiredRetention);
  const result = scheduler.repeat(newCard, new Date());
  const grade = mapRating(rating);
  return result[grade].card.scheduled_days;
};
