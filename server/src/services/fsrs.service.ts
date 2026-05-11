/**
 * Fixed-cycle spaced repetition scheduler.
 *
 * Replaces FSRS with a deterministic review schedule defined by REVIEW_CYCLE.
 * Each entry in the cycle is the number of days to wait before the next review.
 * After completing all reviews in the cycle, the word is considered memorised
 * and is excluded from future reviews (isIgnored = true on the entity).
 *
 * DB column mapping (reusing existing FSRS column names to avoid schema migration):
 *   stability           → cycleStep  (number of reviews completed)
 *   currentIntervalDays → currentIntervalDays (unchanged)
 *   nextReviewDate      → nextReviewDate (unchanged)
 *   lastReviewDate      → lastReviewDate (unchanged)
 *   reviewHistoryJson   → JSON array of ReviewHistoryEntry
 */

// ────────────────────────────────────────────────────────────────────────────
// Cycle definition
// ────────────────────────────────────────────────────────────────────────────

/**
 * Fixed review interval schedule (days between consecutive reviews).
 *
 * Index N is the gap in days between review N and review N+1.
 * The word starts due immediately (step 0). After each completed review the
 * next one is scheduled at `now + REVIEW_CYCLE[currentStep]` days.
 * When `cycleStep` reaches `REVIEW_CYCLE.length` the word is memorised.
 */
export const REVIEW_CYCLE: readonly number[] = [
  1, 1, 1, 1, 1,
  2, 1, 1,
  4, 1, 1,
  8, 1, 1,
  20, 1, 1, 1,
  60, 1, 1, 1,
  150, 1, 1, 1
];

// ────────────────────────────────────────────────────────────────────────────
// Types
// ────────────────────────────────────────────────────────────────────────────

/** A single entry in the review history. */
export interface ReviewHistoryEntry {
  /** ISO date-time of the review. */
  date: string;
  /** Cycle step reached after this review (equals total completed reviews). */
  step: number;
}

/**
 * Persisted review state for a vocabulary word using the fixed-cycle scheduler.
 * Stored on VocabularyEntity via individual columns + reviewHistoryJson.
 */
export interface ReviewState {
  /** Number of reviews completed so far (0 = never reviewed). */
  cycleStep: number;
  /** Interval in days used to schedule the current review slot. */
  currentIntervalDays: number;
  /** ISO date-time of the next scheduled review. */
  nextReviewDate: string;
  /** ISO date-time of the last completed review, or null if never reviewed. */
  lastReviewDate: string | null;
  /** Ordered list of completed review events. */
  reviewHistory: ReviewHistoryEntry[];
}

// ────────────────────────────────────────────────────────────────────────────
// Public API
// ────────────────────────────────────────────────────────────────────────────

/**
 * Creates the initial review state for a newly collected vocabulary word.
 * The first review is scheduled immediately (nextReviewDate = now).
 *
 * @returns A fresh ReviewState at cycleStep 0.
 */
export const createInitialReviewState = (): ReviewState => {
  const nextReview = new Date();
  nextReview.setHours(0, 0, 0, 0);
  return {
    cycleStep: 0,
    currentIntervalDays: 0,
    nextReviewDate: nextReview.toISOString(),
    lastReviewDate: null,
    reviewHistory: []
  };
};

/**
 * Advances the review cycle by one step after the user marks the word as learned.
 *
 * - Increments cycleStep.
 * - Schedules the next review at `now + REVIEW_CYCLE[currentStep]` days.
 * - When the new cycleStep equals `REVIEW_CYCLE.length`, the word is memorised
 *   and the caller should set `isIgnored = true` on the entity.
 *
 * @param state - The current ReviewState.
 * @returns Updated state and a `memorized` flag. When `memorized` is true the
 *          caller must mark the vocabulary as ignored.
 */
export const advanceCycleStep = (
  state: ReviewState
): { state: ReviewState; memorized: boolean } => {
  const now = new Date();
  const nextStep = state.cycleStep + 1;

  const historyEntry: ReviewHistoryEntry = {
    date: now.toISOString(),
    step: nextStep
  };

  const updatedHistory = [...state.reviewHistory, historyEntry];

  if (nextStep >= REVIEW_CYCLE.length) {
    // All reviews completed — word is memorised
    return {
      state: {
        cycleStep: nextStep,
        currentIntervalDays: 0,
        nextReviewDate: now.toISOString(),
        lastReviewDate: now.toISOString(),
        reviewHistory: updatedHistory
      },
      memorized: true
    };
  }

  const intervalDays = REVIEW_CYCLE[state.cycleStep];
  const nextReviewDate = new Date(now);
  nextReviewDate.setDate(nextReviewDate.getDate() + intervalDays);
  nextReviewDate.setHours(0, 0, 0, 0);

  return {
    state: {
      cycleStep: nextStep,
      currentIntervalDays: intervalDays,
      nextReviewDate: nextReviewDate.toISOString(),
      lastReviewDate: now.toISOString(),
      reviewHistory: updatedHistory
    },
    memorized: false
  };
};
