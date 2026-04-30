import fs from "fs";
import path from "path";
import crypto from "crypto";
import { IncludeEnum, type Collection, type EmbeddingFunction } from "chromadb";
import OpenAI from "openai";
import { createChromaClient } from "./chroma-client.factory.js";

// ────────────────────────────────────────────────────────────────────────────
// Types
// ────────────────────────────────────────────────────────────────────────────

export interface ReferenceAudioEntry {
  voice: string;
  emotion: string;
  intensity: string;
  text: string;
  file: string;
}

export interface ReferenceAudioResult {
  file: string;
  text: string;
  emotion: string;
  intensity: string;
  voice: string;
  /** Download URL path relative to server root. */
  downloadUrl: string;
}

export interface ReferenceAudioVectorService {
  /** Index all entries from reference_index.json into ChromaDB. */
  indexAll(): Promise<void>;

  /** Find the best matching reference audio for TTS. */
  query(params: {
    text: string;
    emotion: string;
    intensity: string;
    voiceName: string;
  }): Promise<ReferenceAudioResult | null>;
}

// ────────────────────────────────────────────────────────────────────────────
// Embedding function (reuses OpenAI — same as vector-memory.service.ts)
// ────────────────────────────────────────────────────────────────────────────

class OpenAIEmbeddingFunction implements EmbeddingFunction {
  private openai: OpenAI;
  constructor() {
    this.openai = new OpenAI();
  }

  public async generate(texts: string[]): Promise<number[][]> {
    if (!texts || texts.length === 0) return [];

    const response = await this.openai.embeddings.create({
      model: "text-embedding-3-small",
      input: texts
    });
    return response.data.map((d) => d.embedding);
  }
}

// ────────────────────────────────────────────────────────────────────────────
// Helpers
// ────────────────────────────────────────────────────────────────────────────

const REFERENCE_INDEX_PATH = path.join(process.cwd(), "data", "reference_audios", "reference_index.json");
const COLLECTION_NAME = "reference_audio_index";

/**
 * Builds a deterministic id for a reference audio entry.
 */
const buildRefId = (voice: string, file: string): string => {
  return crypto.createHash("md5").update(`${voice}:${file}`).digest("hex");
};

/**
 * Builds the download URL for a reference audio file.
 */
const buildDownloadUrl = (voice: string, file: string): string => {
  return `/ref-files/${voice}/${file}`;
};

// ────────────────────────────────────────────────────────────────────────────
// Factory
// ────────────────────────────────────────────────────────────────────────────

export interface ReferenceAudioVectorServiceConfig {
  chromaUrl: string;
}

/**
 * Creates a reference audio vector service backed by ChromaDB.
 *
 * @param config - ChromaDB connection settings.
 * @returns The reference audio vector service.
 */
export const createReferenceAudioVectorService = (
  config: ReferenceAudioVectorServiceConfig
): ReferenceAudioVectorService => {
  const client = createChromaClient(config.chromaUrl);
  const embeddingFunction = new OpenAIEmbeddingFunction();
  let collectionPromise: Promise<Collection> | null = null;

  const getCollection = (): Promise<Collection> => {
    if (!collectionPromise) {
      collectionPromise = client.getOrCreateCollection({
        name: COLLECTION_NAME,
        embeddingFunction
      });
    }
    return collectionPromise;
  };

  /**
   * Reads and parses the reference_index.json file.
   */
  const loadIndex = (): ReferenceAudioEntry[] => {
    if (!fs.existsSync(REFERENCE_INDEX_PATH)) {
      console.warn(`[RefAudio] reference_index.json not found at ${REFERENCE_INDEX_PATH}`);
      return [];
    }

    const raw = fs.readFileSync(REFERENCE_INDEX_PATH, "utf-8");
    return JSON.parse(raw) as ReferenceAudioEntry[];
  };

  const indexAll: ReferenceAudioVectorService["indexAll"] = async () => {
    const entries = loadIndex();
    if (entries.length === 0) {
      console.warn("[RefAudio] No entries to index.");
      return;
    }

    const collection = await getCollection();

    // Batch upsert (ChromaDB has a limit of ~5000 per batch)
    const BATCH_SIZE = 500;
    let indexed = 0;

    for (let i = 0; i < entries.length; i += BATCH_SIZE) {
      const batch = entries.slice(i, i + BATCH_SIZE);

      const ids = batch.map((e) => buildRefId(e.voice, e.file));
      const documents = batch.map((e) => e.text);
      const metadatas = batch.map((e) => ({
        voice: e.voice,
        emotion: e.emotion.toLowerCase(),
        intensity: e.intensity.toLowerCase(),
        file: e.file
      }));

      await collection.upsert({ ids, documents, metadatas });
      indexed += batch.length;
    }

    console.log(`[RefAudio] Indexed ${indexed} reference audio entries into ChromaDB.`);
  };

  const query: ReferenceAudioVectorService["query"] = async (params) => {
    const { text, emotion, intensity, voiceName } = params;
    const normalizedEmotion = emotion.toLowerCase();
    const normalizedIntensity = intensity.toLowerCase();

    const collection = await getCollection();

    // Strategy 1: Exact match (emotion + intensity + voice) with semantic text search
    try {
      const results = await collection.query({
        queryTexts: [text],
        nResults: 3,
        where: {
          $and: [
            { voice: { $eq: voiceName } },
            { emotion: { $eq: normalizedEmotion } },
            { intensity: { $eq: normalizedIntensity } }
          ]
        } as any,
        include: [IncludeEnum.documents, IncludeEnum.metadatas, IncludeEnum.distances]
      });

      const ids = results.ids?.[0] ?? [];
      if (ids.length > 0) {
        const meta = results.metadatas?.[0]?.[0] as any;
        const doc = results.documents?.[0]?.[0] as string;
        return {
          file: meta.file,
          text: doc,
          emotion: meta.emotion,
          intensity: meta.intensity,
          voice: meta.voice,
          downloadUrl: buildDownloadUrl(meta.voice, meta.file)
        };
      }
    } catch {
      // Fall through to relaxed search
    }

    // Strategy 2: Relax intensity — match emotion + voice only
    try {
      const results = await collection.query({
        queryTexts: [text],
        nResults: 3,
        where: {
          $and: [
            { voice: { $eq: voiceName } },
            { emotion: { $eq: normalizedEmotion } }
          ]
        } as any,
        include: [IncludeEnum.documents, IncludeEnum.metadatas, IncludeEnum.distances]
      });

      const ids = results.ids?.[0] ?? [];
      if (ids.length > 0) {
        const meta = results.metadatas?.[0]?.[0] as any;
        const doc = results.documents?.[0]?.[0] as string;
        return {
          file: meta.file,
          text: doc,
          emotion: meta.emotion,
          intensity: meta.intensity,
          voice: meta.voice,
          downloadUrl: buildDownloadUrl(meta.voice, meta.file)
        };
      }
    } catch {
      // Fall through to random fallback
    }

    // Strategy 3: Fallback — random entry matching voice only
    const allEntries = loadIndex();
    const voiceEntries = allEntries.filter(
      (e) => e.voice === voiceName && e.emotion.toLowerCase() === normalizedEmotion
    );

    if (voiceEntries.length > 0) {
      const random = voiceEntries[Math.floor(Math.random() * voiceEntries.length)];
      return {
        file: random.file,
        text: random.text,
        emotion: random.emotion,
        intensity: random.intensity,
        voice: random.voice,
        downloadUrl: buildDownloadUrl(random.voice, random.file)
      };
    }

    // Final fallback: any entry with matching voice
    const anyVoiceEntries = allEntries.filter((e) => e.voice === voiceName);
    if (anyVoiceEntries.length > 0) {
      const random = anyVoiceEntries[Math.floor(Math.random() * anyVoiceEntries.length)];
      return {
        file: random.file,
        text: random.text,
        emotion: random.emotion,
        intensity: random.intensity,
        voice: random.voice,
        downloadUrl: buildDownloadUrl(random.voice, random.file)
      };
    }

    return null;
  };

  return { indexAll, query };
};
