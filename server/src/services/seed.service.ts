import type { DataSource, Repository } from "typeorm";
import LevelEntity from "../models/level.entity.js";

const DEFAULT_LEVELS: Array<Pick<LevelEntity, "level" | "maxWords" | "descript" | "guideline">> = [
  {
    level: "A0",
    maxWords: 3,
    descript: "Starting out: recognition of basic words and sounds.",
    guideline: "Use only simple present tense. Avoid any complex grammar."
  },
  {
    level: "A1",
    maxWords: 5,
    descript: "Basic phrases for familiar topics.",
    guideline: "Use simple sentences. Present tense and basic past. Allowed patterns: -고 싶다, -아/어요."
  },
  {
    level: "A2",
    maxWords: 7,
    descript: "Simple conversation and routine tasks.",
    guideline:
      "Basic A2 compound structures are allowed: -고, -지만, -아서/-어서, -(으)면, -(으)려고. Avoid intermediate-level grammar."
  },
  {
    level: "B1",
    maxWords: 10,
    descript: "Handle everyday situations and short texts.",
    guideline:
      "Use lower-intermediate (B1) grammar. Keep sentences not too long. Allowed patterns: -(으)ㄹ 수 있다, -아/어서, -(으)니까, -기 때문에, -(으)면, -는데, -(으)려고 하다, -(으)면서, -(으)ㄴ/는 것 같다, -아/어도 되다, -아/어야 하다. Avoid B2+ grammar."
  },
  {
    level: "B2",
    maxWords: 12,
    descript: "Discuss abstract topics with some fluency.",
    guideline: "Use advanced grammar. Express opinions and more abstract ideas, but keep replies concise."
  },
  {
    level: "C1",
    maxWords: 15,
    descript: "Understand complex texts and express ideas.",
    guideline: "Use advanced grammar, idiomatic expressions, and nuanced language while staying concise."
  },
  {
    level: "C2",
    maxWords: 20,
    descript: "Near-native understanding and expression.",
    guideline: "Use natural, native-like language. Keep replies concise and helpful for learning."
  }
];

export interface SeedLevelsResult {
  inserted: number;
  updated: number;
}

/**
 * Ensures the default CEFR levels exist and are up to date.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns Counts of inserted/updated levels.
 */
export const seedDefaultLevels = async (dataSource: DataSource): Promise<SeedLevelsResult> => {
  const repository: Repository<LevelEntity> = dataSource.getRepository(LevelEntity);
  let inserted = 0;
  let updated = 0;

  for (const entry of DEFAULT_LEVELS) {
    const existing = await repository.findOne({ where: { level: entry.level } });

    if (!existing) {
      await repository.save(repository.create(entry));
      inserted += 1;
      continue;
    }

    const nextDescript = entry.descript.trim();
    const nextGuideline = entry.guideline.trim();
    const shouldUpdateDescript = existing.descript.trim() !== nextDescript;
    const shouldUpdateGuideline = (existing.guideline ?? "").trim() !== nextGuideline;
    const shouldUpdateMaxWords = existing.maxWords !== entry.maxWords;

    if (shouldUpdateDescript || shouldUpdateGuideline || shouldUpdateMaxWords) {
      await repository.save({
        ...existing,
        descript: nextDescript,
        guideline: nextGuideline,
        maxWords: entry.maxWords
      });
      updated += 1;
    }
  }

  return { inserted, updated };
};
