import { In, type DataSource, type Repository } from "typeorm";
import LevelEntity from "../models/level.entity.js";
import VoiceEntity, { type VoiceModel } from "../models/voice.entity.js";

const DEFAULT_LEVELS: Array<Pick<LevelEntity, "level" | "maxWords" | "descript" | "guideline">> = [
  {
    level: "HSK1",
    maxWords: 5,
    descript: "Basic phrases for familiar topics.",
    guideline: "Use HSK1 grammar only. Focus on very simple sentence patterns such as 是, 有, 在, and basic greetings/questions. Keep sentences short and avoid complex complements."
  },
  {
    level: "HSK2",
    maxWords: 7,
    descript: "Simple conversation and routine tasks.",
    guideline:
      "Use HSK2 grammar. Allowed patterns include 因为...所以..., 虽然...但是..., 一边...一边..., 先...然后..., and common 了 usage. Avoid HSK3+ abstract constructions."
  },
  {
    level: "HSK3",
    maxWords: 10,
    descript: "Handle everyday situations and short texts.",
    guideline:
      "Use HSK3 grammar. Allowed patterns: 把/被 sentences, 越来越..., 除了...以外..., 只要...就..., 即使...也.... Keep sentences concise and avoid HSK4+ density."
  },
  {
    level: "HSK4",
    maxWords: 12,
    descript: "Discuss abstract topics with some fluency.",
    guideline: "Use HSK4 grammar to discuss opinions and abstract topics. Prefer clear logic markers such as 不仅...而且..., 与其...不如..., 既...又.... Keep replies concise."
  },
  {
    level: "HSK5",
    maxWords: 15,
    descript: "Understand complex texts and express ideas.",
    guideline: "Use HSK5 grammar with nuanced connectors and occasional idiomatic expressions (成语) when natural. Maintain clarity and concise sentence flow."
  },
  {
    level: "HSK6",
    maxWords: 20,
    descript: "Near-native understanding and expression.",
    guideline: "Use HSK6 near-native Chinese with precise register control. Keep responses concise, coherent, and pedagogically useful."
  }
];

const LEGACY_LEVEL_ALIASES: Record<string, string[]> = {
  HSK1: ["A0", "A1"],
  HSK2: ["A2"],
  HSK3: ["B1"],
  HSK4: ["B2"],
  HSK5: ["C1"],
  HSK6: ["C2"]
};

export interface SeedLevelsResult {
  inserted: number;
  updated: number;
}

export interface SeedVoicesResult {
  inserted: number;
}

const DEFAULT_VOICES: Array<Pick<VoiceEntity, "model" | "voice">> = [
  { model: "openai", voice: "alloy" },
  { model: "openai", voice: "ballad" },
  { model: "openai", voice: "coral" },
  { model: "openai", voice: "cedar" },
  { model: "openai", voice: "echo" },
  { model: "openai", voice: "fable" },
  { model: "openai", voice: "marin" },
  { model: "openai", voice: "nova" },
  { model: "openai", voice: "onyx" },

  { model: "gemini", voice: "Zephyr" },
  { model: "gemini", voice: "Puck" },
  { model: "gemini", voice: "Charon" },
  { model: "gemini", voice: "Kore" },
  { model: "gemini", voice: "Fenrir" },
  { model: "gemini", voice: "Leda" },
  { model: "gemini", voice: "Orus" },
  { model: "gemini", voice: "Aoede" },
  { model: "gemini", voice: "Callirrhoe" },
  { model: "gemini", voice: "Autonoe" },
  { model: "gemini", voice: "Enceladus" },
  { model: "gemini", voice: "Iapetus" },
  { model: "gemini", voice: "Umbriel" },
  { model: "gemini", voice: "Algieba" },
  { model: "gemini", voice: "Despina" },
  { model: "gemini", voice: "Erinome" },
  { model: "gemini", voice: "Algenib" },
  { model: "gemini", voice: "Rasalgethi" },
  { model: "gemini", voice: "Laomedeia" },
  { model: "gemini", voice: "Achernar" },
  { model: "gemini", voice: "Alnilam" },
  { model: "gemini", voice: "Schedar" },
  { model: "gemini", voice: "Gacrux" },
  { model: "gemini", voice: "Pulcherrima" },
  { model: "gemini", voice: "Achird" },
  { model: "gemini", voice: "Zubenelgenubi" },
  { model: "gemini", voice: "Vindemiatrix" },
  { model: "gemini", voice: "Sadachbia" },
  { model: "gemini", voice: "Sadaltager" },
  { model: "gemini", voice: "Sulafat" }
];

const normalizeVoiceModel = (value: string): VoiceModel => {
  const lower = value.trim().toLowerCase();
  return lower === "gemini" ? "gemini" : "openai";
};

/**
 * Ensures the default HSK levels exist and are up to date.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns Counts of inserted/updated levels.
 */
export const seedDefaultLevels = async (dataSource: DataSource): Promise<SeedLevelsResult> => {
  const repository: Repository<LevelEntity> = dataSource.getRepository(LevelEntity);
  let inserted = 0;
  let updated = 0;

  for (const entry of DEFAULT_LEVELS) {
    let existing = await repository.findOne({ where: { level: entry.level } });

    if (!existing) {
      const legacyAliases = LEGACY_LEVEL_ALIASES[entry.level] ?? [];
      if (legacyAliases.length > 0) {
        existing = await repository.findOne({
          where: {
            level: In(legacyAliases)
          }
        });
      }
    }

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
        level: entry.level,
        descript: nextDescript,
        guideline: nextGuideline,
        maxWords: entry.maxWords
      });
      updated += 1;
    }
  }

  return { inserted, updated };
};

/**
 * Ensures the default TTS voice options exist.
 * Existing rows are kept intact and only missing rows are inserted.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns Count of inserted voices.
 */
export const seedDefaultVoices = async (dataSource: DataSource): Promise<SeedVoicesResult> => {
  const repository: Repository<VoiceEntity> = dataSource.getRepository(VoiceEntity);
  let inserted = 0;

  for (const entry of DEFAULT_VOICES) {
    const model = normalizeVoiceModel(entry.model);
    const voice = entry.voice.trim();
    if (!voice) {
      continue;
    }

    const existing = await repository.findOne({
      where: {
        model,
        voice
      }
    });

    if (existing) {
      continue;
    }

    await repository.save(
      repository.create({
        model,
        voice
      })
    );
    inserted += 1;
  }

  return { inserted };
};
