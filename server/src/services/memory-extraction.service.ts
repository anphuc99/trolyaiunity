import { buildMemoryId, type MemoryImportance, type MemoryItem, type MemoryType } from "./vector-memory.service.js";

// ────────────────────────────────────────────────────────────────────────────
// Types
// ────────────────────────────────────────────────────────────────────────────

/** Raw sidecar fields that the AI may attach to assistant turns. */
export interface MemoryCandidate {
  text: string;
  type: MemoryType;
  importance: MemoryImportance;
  /** Character name when this is a subjective character memory. Null for global memories. */
  actor?: string | null;
}

/** Shape of a parsed assistant turn (mirrors chat controller). */
interface AssistantTurn {
  MessageId?: string;
  CharacterName?: string;
  Text?: string;
  Pinyin?: string;
  Tone?: string;
  Translation?: string;
  ImportantMemoryEn?: string;
  ImportantMemoryType?: string;
  ImportantMemoryImportance?: string;
  /** Character name whose subjective memory this is (first-person voice). */
  ImportantMemoryActor?: string;
  [key: string]: unknown;
}

// ────────────────────────────────────────────────────────────────────────────
// Constants
// ────────────────────────────────────────────────────────────────────────────

const VALID_TYPES: ReadonlySet<string> = new Set<MemoryType>([
  "preference",
  "relationship",
  "story_fact",
  "plan",
  "profile",
  "learning"
]);

const VALID_IMPORTANCE: ReadonlySet<string> = new Set<MemoryImportance>([
  "high",
  "medium"
]);

// ────────────────────────────────────────────────────────────────────────────
// Public API
// ────────────────────────────────────────────────────────────────────────────

/**
 * Extracts memory sidecars from all assistant turns in a reply.
 *
 * Rules:
 * - Global memory (no actor): allowed only on the FIRST item.
 * - Character memory (with actor): allowed on ANY item.
 * - Multiple characters can each emit their own memory in the same reply.
 *
 * @param turns - Parsed assistant turns from the AI reply.
 * @returns Array of valid memory candidates (may be empty).
 */
export const extractMemorySidecars = (turns: AssistantTurn[]): MemoryCandidate[] => {
  const candidates: MemoryCandidate[] = [];

  for (let i = 0; i < turns.length; i++) {
    const turn = turns[i];
    const text = typeof turn.ImportantMemoryEn === "string" ? turn.ImportantMemoryEn.trim() : "";
    const type = typeof turn.ImportantMemoryType === "string" ? turn.ImportantMemoryType.trim().toLowerCase() : "";
    const importance = typeof turn.ImportantMemoryImportance === "string"
      ? turn.ImportantMemoryImportance.trim().toLowerCase()
      : "";
    const actor = typeof turn.ImportantMemoryActor === "string" ? turn.ImportantMemoryActor.trim() : "";

    if (!text || !VALID_TYPES.has(type) || !VALID_IMPORTANCE.has(importance)) {
      continue;
    }

    // Global memory (no actor) is only allowed on the first item
    if (!actor && i > 0) {
      continue;
    }

    candidates.push({
      text,
      type: type as MemoryType,
      importance: importance as MemoryImportance,
      actor: actor || null
    });
  }

  return candidates;
};

/**
 * Determines whether a memory candidate should be persisted.
 *
 * @param candidate - The memory candidate to evaluate.
 * @returns True if the memory meets the storage threshold.
 */
export const shouldStoreMemory = (candidate: MemoryCandidate | null): candidate is MemoryCandidate => {
  if (!candidate) return false;
  return VALID_TYPES.has(candidate.type) && VALID_IMPORTANCE.has(candidate.importance) && candidate.text.length > 0;
};

/**
 * Builds a MemoryItem ready for ChromaDB upsert from a validated candidate.
 *
 * @param candidate - Validated memory candidate.
 * @param userId - Owner user id.
 * @param storyId - Current story id (if any).
 * @param sourceSnippet - Optional original-language snippet for debugging.
 * @returns A fully formed MemoryItem.
 */
export const buildMemoryItemFromCandidate = (
  candidate: MemoryCandidate,
  userId: number,
  storyId?: number | null,
  sourceSnippet?: string | null
): MemoryItem => ({
  id: buildMemoryId(userId, candidate.type, candidate.text),
  text: candidate.text,
  metadata: {
    userId,
    storyId: storyId ?? null,
    type: candidate.type,
    importance: candidate.importance,
    actor: candidate.actor ?? null,
    sourceSnippet: sourceSnippet ?? null,
    createdAt: new Date().toISOString()
  }
});

/**
 * Strips all ImportantMemory* sidecar fields from every turn in the assistant
 * reply JSON string. Returns a clean JSON string safe for history storage.
 *
 * @param replyJson - Raw assistant reply JSON string.
 * @returns Cleaned JSON string without memory sidecar fields.
 */
export const stripMemorySidecar = (replyJson: string): string => {
  const trimmed = replyJson.trim();
  if (!trimmed) return trimmed;

  try {
    const parsed = JSON.parse(trimmed) as unknown;
    const items = Array.isArray(parsed) ? parsed : [parsed];

    const cleaned = items.map((item: Record<string, unknown>) => {
      if (!item || typeof item !== "object") return item;

      const copy = { ...item };
      delete copy.ImportantMemoryEn;
      delete copy.ImportantMemoryType;
      delete copy.ImportantMemoryImportance;
      delete copy.ImportantMemoryActor;
      return copy;
    });

    return JSON.stringify(cleaned);
  } catch {
    // If parsing fails, return original — don't break the reply
    return replyJson;
  }
};
