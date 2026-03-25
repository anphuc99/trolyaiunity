import type { CheapAIService } from "./cheap-ai.service.js";
import type { VectorMemoryService, MemoryQueryResult } from "./vector-memory.service.js";

// ────────────────────────────────────────────────────────────────────────────
// Types
// ────────────────────────────────────────────────────────────────────────────

export interface MemoryRetrievalService {
  /**
   * Retrieves and compresses long-term memories relevant to the current
   * user message. Returns a concise English brief ready to inject
   * into the system prompt.
   *
   * @param userId - Authenticated user id.
   * @param userMessage - Latest user message (any language).
   * @param recentHistory - Last 3-5 chat turns for context.
   * @param options - Optional filters.
   * @returns Compressed English memory brief string, or empty string if nothing relevant.
   */
  retrieveMemoryBrief: (
    userId: number,
    userMessage: string,
    recentHistory: Array<{ role: string; content: string }>,
    options?: { storyId?: number | null; topK?: number }
  ) => Promise<string>;
}

export interface MemoryRetrievalServiceConfig {
  vectorMemory: VectorMemoryService;
  cheapAI: CheapAIService;
}

// ────────────────────────────────────────────────────────────────────────────
// Factory
// ────────────────────────────────────────────────────────────────────────────

/**
 * Creates the memory retrieval service that orchestrates:
 * 1. Cheap AI rewrites user intent into English search queries
 * 2. ChromaDB semantic search
 * 3. Cheap AI compresses results into a brief
 *
 * @param config - Required dependencies.
 * @returns The memory retrieval service.
 */
export const createMemoryRetrievalService = (
  config: MemoryRetrievalServiceConfig
): MemoryRetrievalService => {
  const { vectorMemory, cheapAI } = config;

  const retrieveMemoryBrief: MemoryRetrievalService["retrieveMemoryBrief"] = async (
    userId,
    userMessage,
    recentHistory,
    options
  ) => {
    const trimmedMessage = userMessage.trim();
    if (!trimmedMessage) return "";

    // Step 1: Build recent turns text for context
    const recentTurns = recentHistory
      .filter((msg) => msg.role === "user" || msg.role === "assistant")
      .slice(-6)
      .map((msg) => `${msg.role}: ${msg.content.slice(0, 200)}`);

    // Step 2: Cheap AI rewrites user intent into English search queries
    let intents: string[];
    try {
      intents = await cheapAI.rewriteRetrievalIntents(trimmedMessage, recentTurns);
    } catch (error) {
      console.warn("Memory retrieval: intent rewrite failed, using raw message.", error);
      intents = [trimmedMessage.slice(0, 100)];
    }

    if (!intents.length) return "";

    // Step 3: Query ChromaDB for each intent, deduplicate
    const topK = options?.topK ?? 3;
    const seenIds = new Set<string>();
    const allResults: MemoryQueryResult[] = [];

    for (const intent of intents) {
      try {
        const results = await vectorMemory.query(userId, intent, {
          storyId: options?.storyId,
          topK
        });

        for (const result of results) {
          if (!seenIds.has(result.id)) {
            seenIds.add(result.id);
            allResults.push(result);
          }
        }
      } catch (error) {
        console.warn(`Memory retrieval: query failed for intent "${intent}".`, error);
      }
    }

    if (!allResults.length) return "";

    // Sort by distance (lower = more relevant) and take top results
    allResults.sort((a, b) => a.distance - b.distance);
    const topResults = allResults.slice(0, topK * 2);

    // Step 4: Cheap AI compresses retrieved memories into a brief
    const docs = topResults.map((r) => ({
      text: r.text,
      type: r.metadata?.type ?? "unknown",
      importance: r.metadata?.importance ?? "medium"
    }));

    try {
      const brief = await cheapAI.compressMemoryBrief(docs, trimmedMessage);
      return brief === "No relevant memories." ? "" : brief;
    } catch (error) {
      console.warn("Memory retrieval: compression failed, using raw results.", error);
      // Fallback: return raw bullet points
      return topResults
        .map((r) => `- ${r.text}`)
        .join("\n");
    }
  };

  return { retrieveMemoryBrief };
};
