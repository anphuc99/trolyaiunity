import type { Request, Response } from "express";
import type { DataSource, Repository } from "typeorm";
import StreakEntity from "../../models/streak.entity.js";

interface StreakResponse {
  currentStreak: number;
  longestStreak: number;
  lastCompletedDate: string | null;
}

interface StreakController {
  getStreak: (request: Request, response: Response) => Promise<void>;
}

/**
 * Builds the Streak controller.
 *
 * @param dataSource - Initialised TypeORM data source.
 * @returns Streak controller handlers.
 */
export const createStreakController = (dataSource: DataSource): StreakController => {
  const streakRepo: Repository<StreakEntity> = dataSource.getRepository(StreakEntity);
  const toDateKey = (value: Date | string) =>
    new Date(value).toLocaleDateString("en-CA", { timeZone: "Asia/Ho_Chi_Minh" });

  /**
   * Resets the current streak if the last completion is not from yesterday or today.
   */
  const normalizeStreakForDate = async (streak: StreakEntity) => {
    if (!streak.lastCompletedDate) {
      return streak;
    }

    const today = new Date();
    const todayKey = toDateKey(today);
    const yesterday = new Date(today);
    yesterday.setDate(yesterday.getDate() - 1);
    const yesterdayKey = toDateKey(yesterday);
    const lastKey = toDateKey(streak.lastCompletedDate);

    if (lastKey === todayKey || lastKey === yesterdayKey) {
      return streak;
    }

    streak.currentStreak = 0;
    return streakRepo.save(streak);
  };

  /**
   * Returns the streak status for the authenticated user.
   */
  const getStreak: StreakController["getStreak"] = async (request, response) => {
    const userId = request.user?.id;

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      let streak = await streakRepo.findOne({ where: { userId } });

      if (!streak) {
        streak = streakRepo.create({
          userId,
          currentStreak: 0,
          longestStreak: 0,
          lastCompletedDate: null
        });
        streak = await streakRepo.save(streak);
      }

      const normalized = await normalizeStreakForDate(streak);
      const payload: StreakResponse = {
        currentStreak: normalized.currentStreak,
        longestStreak: normalized.longestStreak,
        lastCompletedDate: normalized.lastCompletedDate
          ? normalized.lastCompletedDate.toISOString()
          : null
      };

      response.json(payload);
    } catch (error) {
      console.error("Failed to load streak.", error);
      response.status(500).json({ message: "Không thể tải streak" });
    }
  };

  return {
    getStreak
  };
};
