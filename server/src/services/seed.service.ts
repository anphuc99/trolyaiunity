import type { DataSource, Repository } from "typeorm";
import LevelEntity from "../models/level.entity.js";
import VoiceEntity, { type VoiceModel } from "../models/voice.entity.js";

const DEFAULT_LEVELS: Array<Pick<LevelEntity, "level" | "maxWords" | "descript" | "guideline" | "vocabulary">> = [
  {
    level: "A0",
    maxWords: 3,
    descript: "Starting out: recognition of basic words and sounds.",
    guideline: "Use only very simple HSK 1 patterns: 是, 有, 在, and basic greetings. Keep sentence structure short and avoid complex particles.",
    vocabulary: "你好,谢谢,再见,是,不"
  },
  {
    level: "A1",
    maxWords: 5,
    descript: "Basic phrases for familiar topics.",
    guideline: "Use simple HSK 1-2 grammar. Allowed patterns: 是...的, 想..., 在...呢, 会..., 可以.... Avoid advanced complements and long clauses.",
    vocabulary: "我,你,他,喜欢,学习"
  },
  {
    level: "A2",
    maxWords: 7,
    descript: "Simple conversation and routine tasks.",
    guideline:
      "Use HSK 2-3 compound structures: 因为...所以..., 虽然...但是..., 一边...一边..., 先...然后.... Allow basic 了/过/着 usage, avoid advanced abstract constructions.",
    vocabulary: "今天,昨天,明天,一起,因为"
  },
  {
    level: "B1",
    maxWords: 10,
    descript: "Handle everyday situations and short texts.",
    guideline:
      "Use lower-intermediate HSK 4 grammar. Allowed patterns: 把/被 sentences, 越来越..., 除了...以外..., 只要...就..., 即使...也.... Keep sentences concise and avoid HSK 5+ idiomatic density.",
    vocabulary: "计划,准备,参加,练习,进步"
  },
  {
    level: "B2",
    maxWords: 12,
    descript: "Discuss abstract topics with some fluency.",
    guideline: "Use HSK 5 grammar to discuss opinions and abstract topics. Prefer clear logic markers such as 不仅...而且..., 与其...不如..., 既...又.... Keep replies concise.",
    vocabulary: "观点,经验,影响,分析,原因"
  },
  {
    level: "C1",
    maxWords: 15,
    descript: "Understand complex texts and express ideas.",
    guideline: "Use HSK 6-level grammar with nuanced connectors and occasional idiomatic expressions (成语) when natural. Maintain clarity and concise sentence flow.",
    vocabulary: "策略,判断,比较,细节,表达"
  },
  {
    level: "C2",
    maxWords: 20,
    descript: "Near-native understanding and expression.",
    guideline: "Use near-native, HSK 6+ natural Chinese with precise register control. Keep responses concise, coherent, and pedagogically useful.",
    vocabulary: "语境,隐喻,推理,辩论,连贯"
  }
];

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
    const nextVocabulary = entry.vocabulary.trim();
    const shouldUpdateDescript = existing.descript.trim() !== nextDescript;
    const shouldUpdateGuideline = (existing.guideline ?? "").trim() !== nextGuideline;
    const shouldUpdateVocabulary = (existing.vocabulary ?? "").trim() !== nextVocabulary;
    const shouldUpdateMaxWords = existing.maxWords !== entry.maxWords;

    if (shouldUpdateDescript || shouldUpdateGuideline || shouldUpdateVocabulary || shouldUpdateMaxWords) {
      await repository.save({
        ...existing,
        descript: nextDescript,
        guideline: nextGuideline,
        vocabulary: nextVocabulary,
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
