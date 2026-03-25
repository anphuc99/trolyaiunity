import { GoogleGenerativeAI } from "@google/generative-ai";

// ────────────────────────────────────────────────────────────────────────────
// Types
// ────────────────────────────────────────────────────────────────────────────

export interface CheapAIService {
  /**
   * Rewrites a user message (possibly in Chinese/Vietnamese) plus recent
   * conversation context into 2-4 concise English retrieval intents
   * suitable for semantic vector search.
   *
   * @param userMessage - Latest user message in any language.
   * @param recentTurns - Last 3-5 conversation turns for context.
   * @returns Array of English retrieval intent strings.
   */
  rewriteRetrievalIntents: (
    userMessage: string,
    recentTurns: string[]
  ) => Promise<string[]>;

  /**
   * Compresses a list of retrieved memory documents into a concise
   * English brief (max ~200 words) for the main AI to use.
   *
   * @param docs - Retrieved memory documents with metadata.
   * @param userMessage - Latest user message for relevance filtering.
   * @returns A compressed English memory brief string.
   */
  compressMemoryBrief: (
    docs: Array<{ text: string; type: string; importance: string }>,
    userMessage: string
  ) => Promise<string>;
}

export interface CheapAIServiceConfig {
  /** Google AI API key. Falls back to GOOGLE_API_KEY env var. */
  apiKey?: string;
  /** Model identifier. Defaults to "gemini-2.0-flash-lite". */
  model?: string;
}

// ────────────────────────────────────────────────────────────────────────────
// Prompts
// ────────────────────────────────────────────────────────────────────────────

const INTENT_REWRITE_PROMPT = `You are a search query rewriter for a memory retrieval system.

Given a user message (which may be in Chinese, Vietnamese, or English) and recent conversation context, generate 2 to 4 concise English search queries that capture the user's intent and any implicit needs.

Focus on:
- What the user is asking about or suggesting
- What background knowledge would be helpful (preferences, past events, relationships, plans)
- Implicit needs that the user does not state directly

Rules:
- Output ONLY a JSON array of strings. No markdown, no explanation.
- Each query should be a short English phrase (3-10 words).
- Do NOT translate literally; rewrite for semantic search relevance.

Example:
User says (in Chinese): "我们出去吃饭吧"
Recent context: casual chat about weekend plans
Output: ["dining suggestion based on food preferences", "favorite restaurants or food types", "previous plans to eat out together"]`;

const COMPRESS_BRIEF_PROMPT = `You are a memory compression assistant.

Given a list of retrieved long-term memory facts and the current user message, produce a concise English brief (max 200 words) containing ONLY the memories that are relevant to the current conversation.

Rules:
- Keep each fact as a short bullet point.
- Remove duplicates and irrelevant memories.
- Order by relevance to the current user message.
- If no memories are relevant, output exactly: "No relevant memories."
- Output ONLY the brief text. No markdown headers, no explanation.`;

// ────────────────────────────────────────────────────────────────────────────
// Factory
// ────────────────────────────────────────────────────────────────────────────

/**
 * Creates a cheap AI service for memory retrieval tasks.
 * Uses a lightweight Gemini model for low-cost, fast inference.
 *
 * @param config - Service configuration.
 * @returns The cheap AI service.
 */
export const createCheapAIService = (config: CheapAIServiceConfig = {}): CheapAIService => {
  const apiKey = config.apiKey ?? process.env.GOOGLE_API_KEY ?? "";
  const model = config.model ?? process.env.CHEAP_AI_MODEL ?? "gemini-2.0-flash-lite";

  if (!apiKey) {
    throw new Error("Cheap AI service requires GOOGLE_API_KEY");
  }

  const genAI = new GoogleGenerativeAI(apiKey);

  const rewriteRetrievalIntents: CheapAIService["rewriteRetrievalIntents"] = async (
    userMessage,
    recentTurns
  ) => {
    const generativeModel = genAI.getGenerativeModel({ model });
    const contextBlock = recentTurns.length
      ? `\nRecent conversation:\n${recentTurns.join("\n")}\n`
      : "";

    const prompt = `${INTENT_REWRITE_PROMPT}\n${contextBlock}\nUser message: ${userMessage}\n\nOutput:`;

    const result = await generativeModel.generateContent(prompt);
    const text = result.response.text().trim();

    try {
      const parsed = JSON.parse(text) as unknown;
      if (Array.isArray(parsed)) {
        return parsed
          .filter((item): item is string => typeof item === "string" && item.trim().length > 0)
          .map((item) => item.trim())
          .slice(0, 4);
      }
    } catch {
      // Fallback: split by newlines if JSON parse fails
      return text
        .split(/\n/)
        .map((line) => line.replace(/^[-*\d.)\]]+\s*/, "").trim())
        .filter((line) => line.length > 0)
        .slice(0, 4);
    }

    // Final fallback: use the user message itself as a single intent
    return [userMessage.slice(0, 100)];
  };

  const compressMemoryBrief: CheapAIService["compressMemoryBrief"] = async (docs, userMessage) => {
    if (!docs.length) return "No relevant memories.";

    const generativeModel = genAI.getGenerativeModel({ model });
    const memoriesBlock = docs
      .map((doc, i) => `${i + 1}. [${doc.type}/${doc.importance}] ${doc.text}`)
      .join("\n");

    const prompt = `${COMPRESS_BRIEF_PROMPT}\n\nRetrieved memories:\n${memoriesBlock}\n\nCurrent user message: ${userMessage}\n\nBrief:`;

    const result = await generativeModel.generateContent(prompt);
    const brief = result.response.text().trim();

    return brief || "No relevant memories.";
  };

  return { rewriteRetrievalIntents, compressMemoryBrief };
};
