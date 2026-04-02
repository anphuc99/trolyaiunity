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
   * @param activeCharacters - Names of characters currently in the scene.
   * @returns Array of English retrieval intent strings.
   */
  rewriteRetrievalIntents: (
    userMessage: string,
    recentTurns: string[],
    activeCharacters?: string[]
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

  /**
   * Summarizes the emotional impact of a chat session on active characters.
   *
   * @param chatHistory - Recent chat turns.
   * @param activeCharacters - Names of characters active in the session.
   * @returns English summary of emotional events and impacts.
   */
  summarizeSessionImpact: (
    chatHistory: string[],
    activeCharacters: string[]
  ) => Promise<string>;

  /**
   * Generates 4-8 retrieval questions for ChromaDB to gather evidence
   * before evaluating a relationship update.
   *
   * @param sessionSummary - Output of summarizeSessionImpact.
   * @param ownerName - Character who feels the relationship.
   * @param targetName - Target of the relationship (user name or character name).
   * @param relationshipKind - e.g. "younger_sister", "friend".
   * @param stableThought - Current stable thought text.
   * @param stableEmotion - Current stable emotion value (0-10).
   * @returns Array of English retrieval query strings.
   */
  generateDecisionQueries: (
    sessionSummary: string,
    ownerName: string,
    targetName: string,
    relationshipKind: string,
    stableThought: string,
    stableEmotion: number
  ) => Promise<string[]>;

  /**
   * Evaluates how a relationship should be updated after a session,
   * given the session summary, retrieved memories, and current state.
   *
   * @param input - All context needed for evaluation.
   * @returns Structured update result.
   */
  evaluateRelationshipUpdate: (input: RelationshipEvalInput) => Promise<RelationshipUpdateResult>;

  /**
   * Translates a single Chinese vocabulary word into Vietnamese meaning and pinyin.
   * Used as a fallback when the vocabulary database has no pinyin/meaning.
   *
   * @param word - A Chinese word or short phrase.
   * @returns Object with pinyin and vietnamese fields.
   */
  translateVocabulary: (word: string) => Promise<VocabularyTranslation>;

  /**
   * Converts Chinese text into Mandarin pinyin (tone marks).
   *
   * @param text - Chinese text to convert.
   * @returns Pinyin text.
   */
  transliterateChineseToPinyin: (text: string) => Promise<string>;

  /**
   * Converts Chinese text into IPA pronunciation.
   *
   * @param text - Chinese text to convert.
   * @returns IPA text.
   */
  transliterateChineseToIpa: (text: string) => Promise<string>;
}

/** Output of translateVocabulary. */
export interface VocabularyTranslation {
  pinyin: string;
  vietnamese: string;
}

/** Input for evaluateRelationshipUpdate. */
export interface RelationshipEvalInput {
  sessionSummary: string;
  retrievedMemories: string;
  ownerName: string;
  targetName: string;
  relationshipKind: string;
  stableThought: string;
  temporaryThought: string | null;
  stableEmotion: number;
  currentEmotion: number;
  emotionCause: string | null;
  personality: string;
}

/** Output of evaluateRelationshipUpdate. */
export interface RelationshipUpdateResult {
  stableEmotionDelta: number;
  newCurrentEmotion: number;
  newTemporaryThought: string;
  emotionCause: string;
  stableThoughtRewrite: "none" | "light" | "medium" | "strong";
  newStableThought?: string;
  isShockEvent: boolean;
  reasoning: string;
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

Given a user message (which may be in Chinese, Vietnamese, or English), recent conversation context, and (optionally) a list of active characters in the scene, generate 2 to 4 concise English search queries that capture the user's intent and any implicit needs.

Focus on:
- What the user is asking about or suggesting
- What background knowledge would be helpful (preferences, past events, relationships, plans)
- Implicit needs that the user does not state directly
- If active characters are provided, include at least one query related to the character(s) — e.g. their preferences, personality traits, past interactions, or relationship with the user

Rules:
- Output ONLY a JSON array of strings. No markdown, no explanation.
- Each query should be a short English phrase (3-10 words).
- Do NOT translate literally; rewrite for semantic search relevance.

Example:
User says (in Chinese): "我们出去吃饭吧"
Active characters: Mimi
Recent context: casual chat about weekend plans
Output: ["dining suggestion based on food preferences", "favorite restaurants or food types", "Mimi's food preferences and dining habits", "previous plans to eat out together"]`;

const COMPRESS_BRIEF_PROMPT = `You are a memory compression assistant.

Given a list of retrieved long-term memory facts and the current user message, produce a concise English brief (max 200 words) containing ONLY the memories that are relevant to the current conversation.

Rules:
- Keep each fact as a short bullet point.
- Remove duplicates and irrelevant memories.
- Order by relevance to the current user message.
- If no memories are relevant, output exactly: "No relevant memories."
- Output ONLY the brief text. No markdown headers, no explanation.`;

const SESSION_IMPACT_PROMPT = `You are an emotional analyst for a role-play AI system.

Given a chat session transcript and a list of active character names, summarize the emotional impact of this session on each character.

Focus on:
- Key emotional events (compliments, conflicts, revelations, promises, betrayals, affection)
- Interpersonal dynamics that shifted during the session
- How each character likely FEELS at the end of the session
- Any shock events (sudden betrayal, unexpected confession, traumatic news)

Rules:
- Write in English, third person.
- Keep the summary under 300 words.
- Be specific about which character is affected and how.
- Output ONLY the summary text. No markdown, no JSON.`;

const DECISION_QUERIES_PROMPT = `You are a retrieval query generator for an emotional memory system.

Given a session summary and a character relationship, generate 4-8 English search queries to retrieve evidence from the long-term memory database before deciding how the relationship should change.

The queries MUST cover these 4 categories:
1. REPETITION HISTORY: Has this kind of event happened before between these two?
2. LONG-TERM TRUST: Evidence of deep trust or distrust built over many sessions.
3. RECONCILIATION PRECEDENT: Have they recovered from similar conflicts before?
4. SHOCK EVENT EVIDENCE: Is there evidence that this session was truly extraordinary?

Rules:
- Include both confirming and disconfirming queries (e.g. "times X forgave Y" AND "times X refused to forgive Y").
- Each query should be a short English phrase (5-15 words).
- Queries must be specific to the owner→target pair.
- Output ONLY a JSON array of strings. No markdown, no explanation.`;

const RELATIONSHIP_EVAL_PROMPT = `You are a relationship evaluation engine for a role-play AI system.

Given a session summary, retrieved long-term memories, and the current relationship state, decide how the relationship should be updated.

REWRITE POLICY FOR STABLE THOUGHT:
- Compute emotionDelta = abs(stableEmotionDelta)
- If emotionDelta < 0.5 → stableThoughtRewrite: "none" (no change to stable thought)
- If 0.5 ≤ emotionDelta < 1.5 → stableThoughtRewrite: "light" (soften absolutes, keep core meaning)
- If 1.5 ≤ emotionDelta < 3.0 → stableThoughtRewrite: "medium" (change belief direction, keep emotional tone)
- If emotionDelta ≥ 3.0 OR isShockEvent → stableThoughtRewrite: "strong" (full rewrite of core belief)

ADDITIONAL CHECKS:
- If the stable thought wording contradicts the emotion zone (e.g. thought says "I hate them" but stableEmotion > 7), flag for medium+ rewrite.
- stableEmotionDelta should be small (usually -1.0 to +1.0 per session). Only shock events justify larger swings.
- newCurrentEmotion is volatile and can swing freely (0-10).
- isShockEvent should be true ONLY for genuinely extraordinary events (betrayal, life-saving, confession of love, serious trauma).

Rules:
- Output ONLY valid JSON matching the schema below. No markdown, no explanation.
- "reasoning" should be 1-3 sentences explaining your logic.

Output JSON schema:
{
  "stableEmotionDelta": number,
  "newCurrentEmotion": number,
  "newTemporaryThought": string,
  "emotionCause": string,
  "stableThoughtRewrite": "none" | "light" | "medium" | "strong",
  "newStableThought": string | undefined,
  "isShockEvent": boolean,
  "reasoning": string
}`;

const VOCABULARY_TRANSLATE_PROMPT = `You are a Chinese-Vietnamese dictionary assistant.

Given a Chinese word or short phrase, return its pinyin (with tone marks) and Vietnamese meaning.

Rules:
- Output ONLY valid JSON with two fields: "pinyin" and "vietnamese".
- pinyin must use tone marks (e.g. "ài", "nǐ hǎo"), NOT tone numbers.
- vietnamese must be a concise translation (1-5 words).
- No markdown, no explanation, no extra fields.

Example:
Input: 爱
Output: {"pinyin": "ài", "vietnamese": "yêu"}

Example:
Input: 你好
Output: {"pinyin": "nǐ hǎo", "vietnamese": "xin chào"}`;

const CHINESE_TO_PINYIN_PROMPT = `You are a Mandarin pronunciation converter.

Convert Chinese text into pinyin with tone marks.

Rules:
- Output ONLY valid JSON with one field: "text".
- Convert all Chinese Hanzi into pinyin with tone marks.
- Keep punctuation and sentence boundaries.
- Keep non-Chinese tokens unchanged.
- Separate pinyin syllables with spaces.
- No markdown, no explanation, no extra fields.

Example:
Input: 你好，我叫米米。
Output: {"text": "nǐ hǎo, wǒ jiào mǐ mǐ."}`;

const CHINESE_TO_IPA_PROMPT = `You are a Mandarin IPA converter.

Convert Chinese text into broad IPA pronunciation for Mandarin Chinese.

Rules:
- Output ONLY valid JSON with one field: "text".
- Convert all Chinese Hanzi into IPA.
- Keep punctuation and sentence boundaries.
- Keep non-Chinese tokens unchanged.
- Use standard IPA symbols for Mandarin.
- No markdown, no explanation, no extra fields.

Example:
Input: 你好，我叫米米。
Output: {"text": "ni˨˩˦ xɑʊ˨˩˦, wɔ˨˩˦ tɕjɑʊ˥˩ mi˨˩˦ mi˨˩˦."}`;

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
  const model = config.model ?? process.env.CHEAP_AI_MODEL ?? "gemini-flash-lite-latest";

  if (!apiKey) {
    throw new Error("Cheap AI service requires GOOGLE_API_KEY");
  }

  const genAI = new GoogleGenerativeAI(apiKey);

  const rewriteRetrievalIntents: CheapAIService["rewriteRetrievalIntents"] = async (
    userMessage,
    recentTurns,
    activeCharacters
  ) => {
    const generativeModel = genAI.getGenerativeModel({ model });
    const contextBlock = recentTurns.length
      ? `\nRecent conversation:\n${recentTurns.join("\n")}\n`
      : "";
    const charactersBlock = activeCharacters?.length
      ? `\nActive characters: ${activeCharacters.join(", ")}\n`
      : "";

    const prompt = `${INTENT_REWRITE_PROMPT}\n${contextBlock}${charactersBlock}\nUser message: ${userMessage}\n\nOutput:`;

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
    console.log("Compressing memory brief with prompt:", prompt);
    const result = await generativeModel.generateContent(prompt);
    const brief = result.response.text().trim();

    return brief || "No relevant memories.";
  };

  /**
   * Summarizes the emotional impact of a chat session on active characters.
   */
  const summarizeSessionImpact: CheapAIService["summarizeSessionImpact"] = async (
    chatHistory,
    activeCharacters
  ) => {
    const generativeModel = genAI.getGenerativeModel({ model });
    const historyBlock = chatHistory.join("\n");
    const prompt = `${SESSION_IMPACT_PROMPT}\n\nActive characters: ${activeCharacters.join(", ")}\n\nChat transcript:\n${historyBlock}\n\nSummary:`;

    const result = await generativeModel.generateContent(prompt);
    return result.response.text().trim();
  };

  /**
   * Generates 4-8 retrieval questions for ChromaDB evidence gathering.
   */
  const generateDecisionQueries: CheapAIService["generateDecisionQueries"] = async (
    sessionSummary,
    ownerName,
    targetName,
    relationshipKind,
    stableThought,
    stableEmotion
  ) => {
    const generativeModel = genAI.getGenerativeModel({ model });
    const contextBlock = [
      `Session summary: ${sessionSummary}`,
      `Relationship: ${ownerName} → ${targetName} (${relationshipKind})`,
      `Stable thought: ${stableThought}`,
      `Stable emotion: ${stableEmotion.toFixed(1)} / 10`
    ].join("\n");

    const prompt = `${DECISION_QUERIES_PROMPT}\n\n${contextBlock}\n\nOutput:`;

    const result = await generativeModel.generateContent(prompt);
    const text = result.response.text().trim();

    try {
      const parsed = JSON.parse(text) as unknown;
      if (Array.isArray(parsed)) {
        return parsed
          .filter((item): item is string => typeof item === "string" && item.trim().length > 0)
          .map((item) => item.trim())
          .slice(0, 8);
      }
    } catch {
      // Fallback: split by newlines
      return text
        .split(/\n/)
        .map((line) => line.replace(/^[-*\d.)\]]+\s*/, "").trim())
        .filter((line) => line.length > 0)
        .slice(0, 8);
    }

    return [];
  };

  /**
   * Evaluates how a relationship should be updated after a session.
   */
  const evaluateRelationshipUpdate: CheapAIService["evaluateRelationshipUpdate"] = async (input) => {
    const generativeModel = genAI.getGenerativeModel({ model });
    const contextBlock = [
      `Session summary: ${input.sessionSummary}`,
      `Retrieved memories:\n${input.retrievedMemories}`,
      `\nRelationship: ${input.ownerName} → ${input.targetName} (${input.relationshipKind})`,
      `Personality: ${input.personality}`,
      `Current state:`,
      `  Stable thought: ${input.stableThought}`,
      `  Temporary thought: ${input.temporaryThought ?? "(none)"}`,
      `  Stable emotion: ${input.stableEmotion.toFixed(1)} / 10`,
      `  Current emotion: ${input.currentEmotion.toFixed(1)} / 10`,
      `  Emotion cause: ${input.emotionCause ?? "(none)"}`
    ].join("\n");

    const prompt = `${RELATIONSHIP_EVAL_PROMPT}\n\n${contextBlock}\n\nOutput:`;

    const result = await generativeModel.generateContent(prompt);
    const text = result.response.text().trim();

    // Strip markdown code fences if present
    const jsonText = text.replace(/^```(?:json)?\s*/i, "").replace(/\s*```$/i, "").trim();

    const parsed = JSON.parse(jsonText) as RelationshipUpdateResult;

    // Validate and clamp values
    return {
      stableEmotionDelta: typeof parsed.stableEmotionDelta === "number" ? parsed.stableEmotionDelta : 0,
      newCurrentEmotion: Math.min(10, Math.max(0, typeof parsed.newCurrentEmotion === "number" ? parsed.newCurrentEmotion : input.currentEmotion)),
      newTemporaryThought: typeof parsed.newTemporaryThought === "string" ? parsed.newTemporaryThought : "",
      emotionCause: typeof parsed.emotionCause === "string" ? parsed.emotionCause : "",
      stableThoughtRewrite: (["none", "light", "medium", "strong"] as const).includes(parsed.stableThoughtRewrite as any)
        ? parsed.stableThoughtRewrite
        : "none",
      newStableThought: typeof parsed.newStableThought === "string" ? parsed.newStableThought : undefined,
      isShockEvent: Boolean(parsed.isShockEvent),
      reasoning: typeof parsed.reasoning === "string" ? parsed.reasoning : ""
    };
  };

  /**
   * Translates a Chinese word into pinyin and Vietnamese meaning.
   */
  const translateVocabulary: CheapAIService["translateVocabulary"] = async (word) => {
    const generativeModel = genAI.getGenerativeModel({ model });
    const prompt = `${VOCABULARY_TRANSLATE_PROMPT}\n\nInput: ${word}\n\nOutput:`;

    const result = await generativeModel.generateContent(prompt);
    const text = result.response.text().trim();
    console.log("Vocabulary translation result:", text);

    const jsonText = text.replace(/^```(?:json)?\s*/i, "").replace(/\s*```$/i, "").trim();
    const parsed = JSON.parse(jsonText) as { pinyin?: string; vietnamese?: string };

    return {
      pinyin: typeof parsed.pinyin === "string" ? parsed.pinyin.trim() : "",
      vietnamese: typeof parsed.vietnamese === "string" ? parsed.vietnamese.trim() : ""
    };
  };

  /**
   * Converts Chinese text into pinyin with tone marks.
   */
  const transliterateChineseToPinyin: CheapAIService["transliterateChineseToPinyin"] = async (text) => {
    const generativeModel = genAI.getGenerativeModel({ model });
    const prompt = `${CHINESE_TO_PINYIN_PROMPT}\n\nInput: ${text}\n\nOutput:`;

    const result = await generativeModel.generateContent(prompt);
    const rawText = result.response.text().trim();
    const jsonText = rawText.replace(/^```(?:json)?\s*/i, "").replace(/\s*```$/i, "").trim();
    const parsed = JSON.parse(jsonText) as { text?: string };
    return typeof parsed.text === "string" ? parsed.text.trim() : "";
  };

  /**
   * Converts Chinese text into IPA pronunciation.
   */
  const transliterateChineseToIpa: CheapAIService["transliterateChineseToIpa"] = async (text) => {
    const generativeModel = genAI.getGenerativeModel({ model });
    const prompt = `${CHINESE_TO_IPA_PROMPT}\n\nInput: ${text}\n\nOutput:`;

    const result = await generativeModel.generateContent(prompt);
    const rawText = result.response.text().trim();
    const jsonText = rawText.replace(/^```(?:json)?\s*/i, "").replace(/\s*```$/i, "").trim();
    const parsed = JSON.parse(jsonText) as { text?: string };
    return typeof parsed.text === "string" ? parsed.text.trim() : "";
  };

  return {
    rewriteRetrievalIntents,
    compressMemoryBrief,
    summarizeSessionImpact,
    generateDecisionQueries,
    evaluateRelationshipUpdate,
    translateVocabulary,
    transliterateChineseToPinyin,
    transliterateChineseToIpa
  };
};
