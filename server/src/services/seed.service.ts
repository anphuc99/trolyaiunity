import type { DataSource, Repository } from "typeorm";
import LevelEntity from "../models/level.entity.js";

const DEFAULT_LEVELS: Array<Pick<LevelEntity, "level" | "maxWords" | "descript" | "guideline">> = [
  {
    level: "A0",
    maxWords: 3,
    descript: "Starting out: recognition of basic Pinyin, simple characters, and sounds.",
    guideline: "Use only simple phrases and greetings. Avoid any complex grammar (Equivalent to early HSK 1)."
  },
  {
    level: "A1",
    maxWords: 5,
    descript: "Basic Chinese phrases for familiar topics.",
    guideline: "Use simple sentences. Allowed patterns: 是...的, 有, 在, and basic measure words. Avoid complex particles (Equivalent to HSK 1-2)."
  },
  {
    level: "A2",
    maxWords: 7,
    descript: "Simple Chinese conversation and routine tasks.",
    guideline: "Basic compound structures are allowed: 因为...所以, 虽然...但是, 的/得/地, and basic 了/过/着 usage. Avoid intermediate-level grammar (Equivalent to HSK 3)."
  },
  {
    level: "B1",
    maxWords: 10,
    descript: "Handle everyday situations and short Chinese texts.",
    guideline: "Use lower-intermediate grammar. Allowed patterns: 把/被 sentences, complements of state/result, 越来越, 只要...就. Keep sentences relatively short. Avoid advanced grammar (Equivalent to HSK 4)."
  },
  {
    level: "B2",
    maxWords: 12,
    descript: "Discuss abstract topics with some fluency in Chinese.",
    guideline: "Use advanced grammar. Express opinions and more abstract ideas, but keep replies concise (Equivalent to HSK 5)."
  },
  {
    level: "C1",
    maxWords: 15,
    descript: "Understand complex texts and express ideas using rich vocabulary.",
    guideline: "Use advanced grammar, idiomatic expressions (成语), and nuanced language while staying concise (Equivalent to HSK 6)."
  },
  {
    level: "C2",
    maxWords: 20,
    descript: "Near-native understanding and expression in Chinese.",
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