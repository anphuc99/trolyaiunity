import { IncludeEnum, type Collection, type EmbeddingFunction } from "chromadb";
import OpenAI from "openai";
import crypto from "crypto";
import { createChromaClient } from "./chroma-client.factory.js";

// ────────────────────────────────────────────────────────────────────────────
// Types
// ────────────────────────────────────────────────────────────────────────────

/** Allowed memory categories. */
export type MemoryType =
  | "preference"
  | "relationship"
  | "story_fact"
  | "plan"
  | "profile"
  | "learning";

/** Allowed importance levels. */
export type MemoryImportance = "high" | "medium";

/** Metadata stored alongside each vector document in ChromaDB. */
export interface MemoryMetadata {
  userId: number;
  storyId?: number | null;
  type: MemoryType;
  importance: MemoryImportance;
  actor?: string | null;
  sourceSnippet?: string | null;
  createdAt: string;
}

/** A single memory item ready for upsert. */
export interface MemoryItem {
  /** Deterministic id (derived from userId + type + text). */
  id: string;
  /** English canonical text describing the fact. */
  text: string;
  /** Structured metadata. */
  metadata: MemoryMetadata;
}

/** Options for querying memory. */
export interface MemoryQueryOptions {
  storyId?: number | null;
  topK?: number;
  types?: MemoryType[];
  /** When true, exclude character-subjective memories (actor != ""). Only return global memories. */
  excludeActorMemories?: boolean;
  /** When set, only return memories belonging to this specific character (actor == value). */
  actor?: string;
}

/** A single result from a memory query. */
export interface MemoryQueryResult {
  id: string;
  text: string;
  metadata: MemoryMetadata;
  distance: number;
}

/** Public interface for the vector memory service. */
export interface VectorMemoryService {
  /** Upserts memory items into the vector store. */
  upsert: (userId: number, items: MemoryItem[]) => Promise<void>;
  /** Semantic search for relevant memories. */
  query: (userId: number, queryText: string, options?: MemoryQueryOptions) => Promise<MemoryQueryResult[]>;
  /** Deletes specific memory documents by id. */
  delete: (userId: number, ids: string[]) => Promise<void>;
}

// ────────────────────────────────────────────────────────────────────────────
// Helpers
// ────────────────────────────────────────────────────────────────────────────

/**
 * Builds a deterministic document id from userId, type, and canonical text.
 * This enables natural dedup — upserting the same fact twice overwrites
 * instead of creating a duplicate.
 *
 * @param userId - Owner user id.
 * @param type - Memory type category.
 * @param text - English canonical text.
 * @returns A stable SHA-256 based id string.
 */
export const buildMemoryId = (userId: number, type: string, text: string): string => {
  const input = `${userId}:${type}:${text.trim().toLowerCase()}`;
  return crypto.createHash("sha256").update(input).digest("hex").slice(0, 32);
};

// ────────────────────────────────────────────────────────────────────────────
// Factory
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

export interface VectorMemoryServiceConfig {
  /** ChromaDB server URL (e.g. "http://localhost:8000"). */
  chromaUrl: string;
  /** Collection name. Defaults to "troly_memories". */
  collectionName?: string;
}

/**
 * Creates a vector memory service backed by ChromaDB.
 *
 * @param config - ChromaDB connection settings.
 * @returns The vector memory service.
 */
export const createVectorMemoryService = (config: VectorMemoryServiceConfig): VectorMemoryService => {
  const client = createChromaClient(config.chromaUrl);
  // Important: Used a different collection name to avoid dimension mismatch with the old local model (384 vs 1536)
  const collectionName = config.collectionName ?? "troly_memories_v2";

  const embeddingFunction = new OpenAIEmbeddingFunction();
  let collectionPromise: Promise<Collection> | null = null;

  /**
   * Lazily initialises the ChromaDB collection (created if missing).
   * Passes the default embedding function explicitly so ChromaDB v3
   * does not throw "No embedding function found".
   */
  const getCollection = (): Promise<Collection> => {
    if (!collectionPromise) {
      collectionPromise = client.getOrCreateCollection({ name: collectionName, embeddingFunction });
    }
    return collectionPromise;
  };

  const upsert: VectorMemoryService["upsert"] = async (userId, items) => {
    if (!items.length) return;

    const collection = await getCollection();

    const ids = items.map((item) => item.id);
    const documents = items.map((item) => item.text);
    const metadatas = items.map((item) => ({
      userId: item.metadata.userId,
      storyId: item.metadata.storyId ?? -1,
      type: item.metadata.type,
      importance: item.metadata.importance,
      actor: item.metadata.actor ?? "",
      sourceSnippet: (item.metadata.sourceSnippet ?? "").slice(0, 500),
      createdAt: item.metadata.createdAt
    }));

    await collection.upsert({ ids, documents, metadatas });
  };

  const query: VectorMemoryService["query"] = async (userId, queryText, options) => {
    const collection = await getCollection();
    const topK = options?.topK ?? 5;

    const whereConditions: Record<string, unknown>[] = [{ userId: { $eq: userId } }];

    if (options?.storyId) {
      whereConditions.push({
        $or: [
          { storyId: { $eq: options.storyId } },
          { storyId: { $eq: -1 } }
        ]
      });
    }

    if (options?.types?.length) {
      whereConditions.push({ type: { $in: options.types } });
    }

    if (options?.excludeActorMemories) {
      whereConditions.push({ actor: { $eq: "" } });
    }

    if (options?.actor) {
      whereConditions.push({ actor: { $eq: options.actor } });
    }

    const where = whereConditions.length === 1
      ? (whereConditions[0] as Record<string, unknown>)
      : { $and: whereConditions } as unknown as Record<string, unknown>;

    const include = [IncludeEnum.documents, IncludeEnum.metadatas, IncludeEnum.distances];

    const results = await collection.query({
      queryTexts: [queryText],
      nResults: topK,
      where: where as any,
      include
    });

    const ids = results.ids?.[0] ?? [];
    const documents = results.documents?.[0] ?? [];
    const metadatas = results.metadatas?.[0] ?? [];
    const distances = results.distances?.[0] ?? [];

    return ids.map((id, i) => ({
      id: id ?? "",
      text: (documents[i] ?? "") as string,
      metadata: (metadatas[i] ?? {}) as unknown as MemoryMetadata,
      distance: (distances[i] ?? 1) as number
    }));
  };

  const deleteFn: VectorMemoryService["delete"] = async (_userId, ids) => {
    if (!ids.length) return;
    const collection = await getCollection();
    await collection.delete({ ids });
  };

  return { upsert, query, delete: deleteFn };
};
