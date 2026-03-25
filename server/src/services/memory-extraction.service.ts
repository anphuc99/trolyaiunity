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
  /** Global/objective memory fields — first item only. */
  GlobalMemoryEn?: string;
  GlobalMemoryType?: string;
  GlobalMemoryImportance?: string;
  /** Character subjective memory fields — any item, actor = CharacterName. */
  ImportantMemoryEn?: string;
  ImportantMemoryType?: string;
  ImportantMemoryImportance?: string;
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
 * - Global memory (GlobalMemory* fields, no actor): only on the FIRST item.
 * - Character memory (ImportantMemory* fields + actor): on ANY item.
 * - Both types can coexist on the first item (different field names).
 * - Multiple characters can each emit their own character memory in the same reply.
 *
 * @param turns - Parsed assistant turns from the AI reply.
 * @returns Array of valid memory candidates (may be empty).
 */
export const extractMemorySidecars = (turns: AssistantTurn[]): MemoryCandidate[] => {
  const candidates: MemoryCandidate[] = [];

  for (let i = 0; i < turns.length; i++) {
    const turn = turns[i];

    // ── Global memory (first item only, GlobalMemory* prefix) ──────────────
    if (i === 0) {
      const gText = typeof turn.GlobalMemoryEn === "string" ? turn.GlobalMemoryEn.trim() : "";
      const gType = typeof turn.GlobalMemoryType === "string" ? turn.GlobalMemoryType.trim().toLowerCase() : "";
      const gImportance = typeof turn.GlobalMemoryImportance === "string" ? turn.GlobalMemoryImportance.trim().toLowerCase() : "";

      if (gText && VALID_TYPES.has(gType) && VALID_IMPORTANCE.has(gImportance)) {
        candidates.push({
          text: gText,
          type: gType as MemoryType,
          importance: gImportance as MemoryImportance,
          actor: null
        });
      }
    }

    // ── Character memory (any item, ImportantMemory* prefix + actor) ────────
    const cText = typeof turn.ImportantMemoryEn === "string" ? turn.ImportantMemoryEn.trim() : "";
    const cType = typeof turn.ImportantMemoryType === "string" ? turn.ImportantMemoryType.trim().toLowerCase() : "";
    const cImportance = typeof turn.ImportantMemoryImportance === "string" ? turn.ImportantMemoryImportance.trim().toLowerCase() : "";
    const cActor = typeof turn.ImportantMemoryActor === "string" ? turn.ImportantMemoryActor.trim() : "";

    if (cText && VALID_TYPES.has(cType) && VALID_IMPORTANCE.has(cImportance) && cActor) {
      candidates.push({
        text: cText,
        type: cType as MemoryType,
        importance: cImportance as MemoryImportance,
        actor: cActor
      });
    }
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
      // Strip global memory fields
      delete copy.GlobalMemoryEn;
      delete copy.GlobalMemoryType;
      delete copy.GlobalMemoryImportance;
      // Strip character memory fields
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
