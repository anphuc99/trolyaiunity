/**
 * Fixed-interval spaced repetition service for MyLog diary entries.
 *
 * Uses a predefined schedule of 20 review intervals spanning ~10 years.
 * Each review advances the entry to the next interval. After all intervals
 * are exhausted the entry is considered fully reviewed (archived).
 */

/**
 * Fixed review intervals in days.
 *
 * 1d → 3d → 1w → 2w → 1m → 2m → 4m → 8m → 1y → 1.5y → 2y → 2.7y
 * → 3.6y → 4.4y → 5.2y → 6y → 6.8y → 7.7y → 8.8y → 10y
 */
export const REVIEW_INTERVALS: readonly number[] = [
  1, 3, 7, 14, 30, 60, 120, 240, 365, 540,
  730, 1000, 1300, 1600, 1900, 2200, 2500, 2800, 3200, 3650
] as const;

/**
 * Total number of scheduled reviews before an entry is archived.
 */
export const MAX_REVIEW_COUNT = REVIEW_INTERVALS.length;

/**
 * Calculates the next review date based on the current review count.
 *
 * @param reviewCount - Number of reviews already completed (0-based index into REVIEW_INTERVALS).
 * @param fromDate - The reference date to calculate offset from (typically now).
 * @returns The next review date, or null if all reviews are complete.
 */
export const calculateNextReviewDate = (reviewCount: number, fromDate: Date): Date | null => {
  if (reviewCount < 0 || reviewCount >= REVIEW_INTERVALS.length) {
    return null;
  }

  const intervalDays = REVIEW_INTERVALS[reviewCount];
  const next = new Date(fromDate.getTime());
  next.setDate(next.getDate() + intervalDays);
  return next;
};

/**
 * Creates the initial review date for a newly created diary entry.
 * The first review is scheduled 1 day after creation.
 *
 * @param createdAt - The creation timestamp of the diary entry.
 * @returns The first review date (createdAt + 1 day).
 */
export const createInitialReviewDate = (createdAt: Date): Date => {
  const next = new Date(createdAt.getTime());
  next.setDate(next.getDate() + 1);
  return next;
};
