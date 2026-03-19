import { In, type DataSource, type Repository } from "typeorm";
import LevelEntity from "../models/level.entity.js";
import VoiceEntity, { type VoiceModel } from "../models/voice.entity.js";

const DEFAULT_LEVELS: Array<Pick<LevelEntity, "level" | "maxWords" | "descript" | "guideline">> = [
  {
    level: "HSK1",
    maxWords: 5,
    descript: "Basic phrases for familiar topics.",
    guideline: "Use HSK 3.0 Level 1 grammar only. Keep sentences extremely short and concrete. Prioritize basic sentence types and core patterns: simple affirmative/negative/interrogative sentences; A是B; 有/没有; 在 + location; this/that/which; basic possession with 的; basic modal verbs such as 想, 会, 能; simple adjective predicates with 很/太; simple verb-object statements; basic time expressions; simple imperative/polite forms with 请. Avoid comparison, serial clauses, complements, passive/disposal structures, abstract connectors, and any idiomatic compression."
  },
  {
    level: "HSK2",
    maxWords: 7,
    descript: "Simple conversation and routine tasks.",
    guideline:
      "Use HSK 3.0 Level 2 grammar. Allow common daily-life expansion beyond Level 1: sentence-final 了 for change/new situation, experiential 过 in simple contexts, progressive 在/正在, simple result/state complements such as 到/见/好, basic comparison with 比, existence/location refinements, choice and sequence markers such as 还是, 或者, 先...再/然后..., frequency/time expressions, and common cause/result patterns in simple form. Use everyday topic sentences only. Avoid 把/被, complex complement chains, concessive structures with heavy subordination, and abstract argumentative writing."
  },
  {
    level: "HSK3",
    maxWords: 10,
    descript: "Handle everyday situations and short texts.",
    guideline:
      "Use HSK 3.0 Level 3 grammar. Allow fuller everyday narration and explanation with intermediate patterns: directional complements, potential complements in common forms, comparison extensions, 连动/兼语 constructions, 越来越..., 一边...一边..., 一...就..., 先...然后..., 因为...所以..., 虽然...但是..., 如果...就..., 除了...以外..., 只要...就..., even basic 把/被 in very clear contexts. Use simple paragraph logic but keep each sentence concise. Avoid dense formal written style, heavy nominalization, advanced rhetorical inversion, and idioms unless extremely common."
  },
  {
    level: "HSK4",
    maxWords: 12,
    descript: "Discuss abstract topics with some fluency.",
    guideline: "Use HSK 3.0 Level 4 grammar. Permit broader discussion, explanation, and opinion with clearer logical structure: 不但/不仅...而且..., 既...又..., 先...再..., 无论...都..., 即使...也..., 与其...不如..., 一方面...另一方面..., as well as more flexible 把/被, complement structures, and topic-comment organization. Sentences may be moderately complex but should remain readable and conversational. Avoid highly literary compression, overly formal bureaucratic phrasing, and HSK5-6 style abstract discourse density."
  },
  {
    level: "HSK5",
    maxWords: 15,
    descript: "Understand complex texts and express ideas.",
    guideline: "Use HSK 3.0 Level 5 grammar. Allow mature discussion, explanation, and argument with richer connectors and nuanced stance marking: 之所以...是因为..., 并非...而是..., 不论/无论..., 既然..., 尽管..., 甚至..., 反而..., 从而..., 以便..., 以免..., 何况..., 况且..., rather complete complement usage, and occasional natural 成语 or书面词 when helpful. Keep logic explicit and elegant, but do not become excessively literary or obscure."
  },
  {
    level: "HSK6",
    maxWords: 20,
    descript: "Near-native understanding and expression.",
    guideline: "Use HSK 3.0 Level 6 grammar with near-native flexibility and precise register control. Allow layered subordination, nuanced discourse markers, formal/informal register shifts, compact but natural argumentation, rhetorical emphasis, advanced complements, and idiomatic expressions when contextually appropriate. Maintain coherence, precision, and pedagogical readability. Avoid archaic classical wording unless explicitly requested."
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
