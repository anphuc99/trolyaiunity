import type { Request, Response } from "express";
import { randomUUID } from "crypto";
import path from "path";
import type { DataSource } from "typeorm";
import { LessThanOrEqual, Between } from "typeorm";
import { createOpenAIChatService, type OpenAIChatService } from "../../services/openai.service.js";
import { createGeminiChatService, isGeminiModel, type GeminiChatService } from "../../services/gemini.service.js";
import { buildMyLogSystemPrompt } from "../../services/mylog-prompt.service.js";
import { createInitialReviewDate, calculateNextReviewDate, MAX_REVIEW_COUNT } from "../../services/mylog-review.service.js";
import MyLogEntity from "../../models/my-log.entity.js";
import MyLogJournalEntity from "../../models/my-log-journal.entity.js";
import MyLogMessageEntity from "../../models/my-log-message.entity.js";
import CharacterEntity from "../../models/character.entity.js";
import UserEntity from "../../models/user.entity.js";
import { createChatHistoryStore, type ChatHistoryMessage, type ChatHistoryStore } from "../../services/chat-history.service.js";
import { buildAudioId } from "../../services/tts.service.js";

/**
 * The special character name used for hidden psychologist evaluation messages.
 * These messages are stored in DB and chat history for AI context but are
 * never sent to the client.
 */
const PSYCHOLOGIST_CHARACTER = "__PsychologistEval";

interface MyLogController {
  createLog: (request: Request, response: Response) => Promise<void>;
  listLogs: (request: Request, response: Response) => Promise<void>;
  updateLog: (request: Request, response: Response) => Promise<void>;
  getLog: (request: Request, response: Response) => Promise<void>;
  getDueLogs: (request: Request, response: Response) => Promise<void>;
  submitReview: (request: Request, response: Response) => Promise<void>;
  sendMessage: (request: Request, response: Response) => Promise<void>;
  getHistory: (request: Request, response: Response) => Promise<void>;
  endConversation: (request: Request, response: Response) => Promise<void>;
  listJournals: (request: Request, response: Response) => Promise<void>;
  getJournal: (request: Request, response: Response) => Promise<void>;
  appendDeveloperMessage: (request: Request, response: Response) => Promise<void>;
  editMessage: (request: Request, response: Response) => Promise<void>;
  getDeveloperState: (request: Request, response: Response) => Promise<void>;
}

interface MyLogControllerDeps {
  openAIService?: OpenAIChatService;
  geminiService?: GeminiChatService;
  historyStore?: ChatHistoryStore;
  /**
   * Optional override for building the system instruction text.
   * Primarily used by unit tests to avoid database lookups.
   */
  systemPromptBuilder?: (params: { userId: number; body: unknown }) => Promise<string> | string;
}

interface AssistantTurn {
  MessageId?: string;
  CharacterName?: string;
  Text?: string;
  Tone?: string;
  Translation?: string;
}

type ChatReplyService = OpenAIChatService | GeminiChatService;

interface JsonReplyResult {
  reply: string;
  model: string;
}

/**
 * Builds the MyLog controller with injected data source dependencies.
 *
 * Handles diary CRUD, fixed-interval spaced repetition reviews,
 * diary-focused AI chat (with psychologist evaluation), and
 * diary chat journal persistence.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @param deps - Optional overrides for external services (testing).
 * @returns The MyLog controller handlers.
 */
export const createMyLogController = (
  dataSource: DataSource,
  deps: MyLogControllerDeps = {}
): MyLogController => {
  const myLogRepository = dataSource.getRepository(MyLogEntity);
  const myLogJournalRepository = dataSource.getRepository(MyLogJournalEntity);
  const myLogMessageRepository = dataSource.getRepository(MyLogMessageEntity);
  const characterRepository = dataSource.getRepository(CharacterEntity);
  const userRepository = dataSource.getRepository(UserEntity);

  // AI model configuration — configurable via .env
  const mylogModel = process.env.MYLOG_AI_MODEL ?? "gemini-3-flash-preview";

  // OpenAI configuration
  const openAIApiKey = process.env.OPENAI_API_KEY ?? "";
  const openAIModel = process.env.OPENAI_MODEL ?? "gpt-4.1-mini";
  const systemPromptPath = process.env.OPENAI_SYSTEM_PROMPT_PATH;
  const openAIService =
    deps.openAIService ?? (openAIApiKey ? createOpenAIChatService({ apiKey: openAIApiKey, model: openAIModel, systemPromptPath }) : null);

  // Gemini configuration
  const geminiApiKey = process.env.GOOGLE_API_KEY ?? "";
  const geminiModel = process.env.GEMINI_MODEL ?? "gemini-2.5-flash";
  const geminiService =
    deps.geminiService ?? (geminiApiKey ? createGeminiChatService({ apiKey: geminiApiKey, model: geminiModel }) : null);

  // Isolated chat history for diary conversations (separate from main story chat)
  const defaultMyLogHistoryDir = process.env.MYLOG_HISTORY_DIR ?? path.join(process.cwd(), "data", "mylog-history");
  const historyStore = deps.historyStore ?? createChatHistoryStore(defaultMyLogHistoryDir);

  let hasLoggedAssistantReplyParseFailure = false;

  const normalizeName = (value: string) => value.trim().toLowerCase();

  // ────────────────────────────────────────────────────────────────────────
  // Helper: get today's start/end boundaries (UTC)
  // ────────────────────────────────────────────────────────────────────────

  /**
   * Returns the start and end of today in UTC.
   *
   * @returns Object with todayStart and todayEnd Date instances.
   */
  const getTodayBounds = () => {
    const now = new Date();
    const todayStart = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), 0, 0, 0, 0));
    const todayEnd = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), 23, 59, 59, 999));
    return { todayStart, todayEnd };
  };

  // ────────────────────────────────────────────────────────────────────────
  // Helper: parse assistant reply JSON
  // ────────────────────────────────────────────────────────────────────────

  /**
   * Parses an assistant reply string into an array of turn objects.
   *
   * @param content - Raw assistant reply content.
   * @returns Array of parsed assistant turns.
   */
  const parseAssistantReply = (content: string): AssistantTurn[] => {
    const trimmed = content.trim();
    if (!trimmed) return [];

    const tryParse = (input: string) => {
      try {
        const parsed = JSON.parse(input) as unknown;
        if (Array.isArray(parsed)) return parsed as AssistantTurn[];
        if (parsed && typeof parsed === "object") return [parsed as AssistantTurn];
      } catch (error) {
        if (!hasLoggedAssistantReplyParseFailure) {
          console.warn("MyLog: Failed to parse assistant reply as JSON; attempting fallback.", error);
          hasLoggedAssistantReplyParseFailure = true;
        }
        return null;
      }
      return null;
    };

    const direct = tryParse(trimmed);
    if (direct) return direct;

    const arrayStart = trimmed.indexOf("[");
    const arrayEnd = trimmed.lastIndexOf("]");
    if (arrayStart !== -1 && arrayEnd > arrayStart) {
      const sliced = tryParse(trimmed.slice(arrayStart, arrayEnd + 1));
      if (sliced) return sliced;
    }

    const objectStart = trimmed.indexOf("{");
    const objectEnd = trimmed.lastIndexOf("}");
    if (objectStart !== -1 && objectEnd > objectStart) {
      const sliced = tryParse(trimmed.slice(objectStart, objectEnd + 1));
      if (sliced) return sliced;
    }

    return [];
  };

  // ────────────────────────────────────────────────────────────────────────
  // Helper: validate assistant turn schema (MyLog-specific)
  // ────────────────────────────────────────────────────────────────────────

  /**
   * Validates that assistant turns match the required JSON schema.
   * For MyLog, the __PsychologistEval character is allowed to have empty Translation.
   *
   * @param turns - Parsed assistant turns.
   * @returns True when all required fields are valid.
   */
  const isValidMyLogTurnSchema = (turns: AssistantTurn[]) => {
    if (!Array.isArray(turns) || turns.length === 0) return false;

    for (const turn of turns) {
      if (!turn || typeof turn !== "object") return false;

      const messageId = typeof turn.MessageId === "string" ? turn.MessageId.trim() : "";
      const characterName = typeof turn.CharacterName === "string" ? turn.CharacterName.trim() : "";
      const text = typeof turn.Text === "string" ? turn.Text.trim() : "";
      const tone = typeof turn.Tone === "string" ? turn.Tone.trim() : "";

      if (!messageId || !characterName || !text || !tone) return false;

      // __PsychologistEval entries may have empty Translation
      const isPsychologist = characterName === PSYCHOLOGIST_CHARACTER;
      if (!isPsychologist) {
        const translation = typeof turn.Translation === "string" ? turn.Translation.trim() : "";
        if (!translation) return false;
      }
    }

    return true;
  };

  // ────────────────────────────────────────────────────────────────────────
  // Helper: JSON retry prompt
  // ────────────────────────────────────────────────────────────────────────

  /**
   * Creates a corrective retry prompt for invalid JSON responses.
   *
   * @param promptSeed - Original user message or context.
   * @returns Retry prompt text.
   */
  const buildJsonRetryPrompt = (promptSeed: string) => {
    return [
      promptSeed,
      "",
      "Your previous response did not match the required JSON format.",
      "Retry now and return ONLY valid JSON array.",
      "Requirements:",
      "- Must be a JSON array (2-10 items).",
      "- Character messages: non-empty MessageId, CharacterName, Text, Tone, Translation.",
      "- The LAST item MUST have CharacterName: \"__PsychologistEval\" with English clinical text in Text field.",
      "- __PsychologistEval may have empty Translation.",
      "- No markdown, no explanations, no comments."
    ].join("\n");
  };

  // ────────────────────────────────────────────────────────────────────────
  // Helper: request AI reply with retry
  // ────────────────────────────────────────────────────────────────────────

  /**
   * Requests a reply from AI and retries when JSON format is invalid.
   *
   * @param service - Target chat service (OpenAI or Gemini).
   * @param message - User message to send.
   * @param history - Message history.
   * @param modelOverride - Optional model override.
   * @param retryLimit - Max retry attempts after initial call.
   * @returns JSON-valid assistant reply result.
   */
  const requestJsonReplyWithRetry = async (
    service: ChatReplyService,
    message: string | undefined,
    history: Array<{ role: "system" | "developer" | "user" | "assistant"; content: string }>,
    modelOverride?: string,
    retryLimit = 2
  ): Promise<JsonReplyResult> => {
    let attempt = 0;
    let prompt = typeof message === "string" && message.trim() ? message.trim() : undefined;
    const retryPromptSeed = prompt || "Continue the diary conversation naturally based on the current chat history.";

    while (attempt <= retryLimit) {
      const result = await service.createReply(prompt, history, modelOverride || undefined);

      const turns = parseAssistantReply(result.reply);
      if (isValidMyLogTurnSchema(turns)) {
        return result;
      }

      attempt += 1;
      if (attempt > retryLimit) break;

      console.warn(`MyLog: Assistant reply JSON invalid at attempt ${attempt}. Retrying.`);
      prompt = buildJsonRetryPrompt(retryPromptSeed);
    }

    throw new Error("AI returned invalid JSON format after retries");
  };

  // ────────────────────────────────────────────────────────────────────────
  // Helper: collect used message IDs from history
  // ────────────────────────────────────────────────────────────────────────

  const collectAssistantMessageIds = (messages: { role: string; content: string }[]) => {
    const ids = new Set<string>();
    for (const message of messages) {
      if (message.role !== "assistant") continue;
      const turns = parseAssistantReply(message.content);
      for (const turn of turns) {
        const messageId = typeof turn.MessageId === "string" ? turn.MessageId.trim() : "";
        if (messageId) ids.add(messageId);
      }
    }
    return ids;
  };

  // ────────────────────────────────────────────────────────────────────────
  // Helper: normalize message IDs to prevent duplicates
  // ────────────────────────────────────────────────────────────────────────

  const normalizeAssistantReplyMessageIds = (reply: string, usedIds: Set<string>) => {
    const turns = parseAssistantReply(reply);
    if (!turns.length) return reply;

    let hasChanged = false;
    const normalizedTurns = turns.map((turn) => {
      const currentId = typeof turn.MessageId === "string" ? turn.MessageId.trim() : "";
      const shouldReplaceId = !currentId || usedIds.has(currentId);

      if (!shouldReplaceId) {
        usedIds.add(currentId);
        return turn;
      }

      let nextId = randomUUID();
      while (usedIds.has(nextId)) nextId = randomUUID();
      usedIds.add(nextId);
      hasChanged = true;
      return { ...turn, MessageId: nextId };
    });

    if (!hasChanged) return reply;
    return JSON.stringify(normalizedTurns);
  };

  // ────────────────────────────────────────────────────────────────────────
  // Helper: developer message formatting (character add/remove, context)
  // ────────────────────────────────────────────────────────────────────────

  /**
   * Formats a developer message for a character being added to the conversation.
   *
   * @param payload - Request body containing character details.
   * @returns Formatted developer message string, or empty when name is missing.
   */
  const formatCharacterAddedMessage = (payload: Record<string, unknown>) => {
    const character = (payload.character ?? {}) as Record<string, unknown>;
    const name = typeof character.name === "string" ? character.name.trim() : "";
    const personality = typeof character.personality === "string" ? character.personality.trim() : "";
    const gender = typeof character.gender === "string" ? character.gender.trim() : "";
    const ageRaw = typeof character.age === "number" ? character.age : Number.parseInt(String(character.age ?? ""), 10);
    const age = Number.isInteger(ageRaw) && ageRaw >= 0 && ageRaw <= 150 ? ageRaw : null;
    const appearance = typeof character.appearance === "string" ? character.appearance.trim() : "";

    if (!name) {
      return "";
    }

    const lines = [`Character \"${name}\" has been added.`];
    if (gender) lines.push(`Gender: ${gender}`);
    if (age != null) lines.push(`Age: ${age}`);
    if (personality) lines.push(`Personality: ${personality}`);
    if (appearance) lines.push(`Appearance: ${appearance}`);

    return lines.join("\n");
  };

  /**
   * Formats a developer message for a character being removed from the conversation.
   *
   * @param payload - Request body containing character details.
   * @returns Formatted developer message string, or empty when name is missing.
   */
  const formatCharacterRemovedMessage = (payload: Record<string, unknown>) => {
    const character = (payload.character ?? {}) as Record<string, unknown>;
    const name = typeof character.name === "string" ? character.name.trim() : "";

    if (!name) {
      return "";
    }

    return [
      `Character \"${name}\" has been removed from this conversation.`,
      "Do not use this character again unless it is added back."
    ].join("\n");
  };

  /**
   * Builds a developer context update message.
   *
   * @param payload - Request payload containing the context string.
   * @returns A formatted developer message or an empty string when missing.
   */
  const formatContextMessage = (payload: Record<string, unknown>) => {
    const context = typeof payload.context === "string" ? payload.context.trim() : "";

    if (!context) {
      return "";
    }

    return ["Developer context update:", context].join("\n");
  };

  /**
   * Formats a developer note for edited assistant messages.
   *
   * @param messageId - Message identifier from the assistant output.
   * @param content - Updated assistant text.
   * @returns A developer message string.
   */
  const formatAssistantEditMessage = (messageId: string, content: string) => {
    const trimmed = messageId.trim();
    if (!trimmed) return "";

    const updatedContent = content.trim();
    if (!updatedContent) return "";

    return [
      `Assistant message edited: ${trimmed}.`,
      "New content:",
      updatedContent
    ].join("\n");
  };

  // ────────────────────────────────────────────────────────────────────────
  // Helper: parse developer character actions and assistant edits
  // ────────────────────────────────────────────────────────────────────────

  /**
   * Parses a developer message to extract character add/remove actions.
   *
   * @param content - Developer message content.
   * @returns Object with character name and active state, or null.
   */
  const parseDeveloperCharacterAction = (content: string) => {
    const addedMatch = content.match(/^Character\s+"([^"]+)"\s+has been added\./m);
    if (addedMatch) {
      return { name: addedMatch[1].trim(), active: true };
    }

    const removedMatch = content.match(/^Character\s+"([^"]+)"\s+has been removed from this conversation\./m);
    if (removedMatch) {
      return { name: removedMatch[1].trim(), active: false };
    }

    return null;
  };

  /**
   * Parses a developer edit note to extract the target message ID and updated text.
   *
   * @param content - Developer message content.
   * @returns Object with messageId and updatedText, or null.
   */
  const parseAssistantEditNote = (content: string) => {
    const englishMatch = content.match(/^Assistant\s+message\s+edited:\s+([^\.\n]+)\./i);
    const vietnameseMatch = content.match(/^Chat\s+co\s+messageID\s+duoc\s+sua\s+thanh\s+([^\.\n]+)\./i);
    const idMatch = englishMatch ?? vietnameseMatch;

    if (!idMatch) return null;

    const messageId = idMatch[1].trim();
    if (!messageId) return null;

    const englishContentMatch = content.match(/New\s+content:\s*([\s\S]+)/i);
    const vietnameseContentMatch = content.match(/Noi\s+dung\s+moi:\s*([\s\S]+)/i);
    const contentMatch = englishContentMatch ?? vietnameseContentMatch;
    const updatedText = contentMatch ? contentMatch[1].trim() : "";
    if (!updatedText) return null;

    return { messageId, updatedText };
  };

  /**
   * Applies developer edit notes to assistant messages in the history.
   * Used by getHistory to reflect edits before returning to the client.
   *
   * @param history - Full chat history.
   * @returns History with edits applied to assistant turns.
   */
  const applyAssistantEdits = (history: { role: string; content: string }[]) => {
    const edits = new Map<string, string>();

    for (const message of history) {
      if (message.role !== "developer") continue;

      const edit = parseAssistantEditNote(message.content);
      if (edit) {
        edits.set(edit.messageId, edit.updatedText);
      }
    }

    if (!edits.size) return history;

    return history.map((message) => {
      if (message.role !== "assistant") return message;

      const turns = parseAssistantReply(message.content);
      if (!turns.length) return message;

      let didUpdate = false;
      const nextTurns = turns.map((turn) => {
        const turnId = typeof turn.MessageId === "string" ? turn.MessageId.trim() : "";
        const updatedText = turnId ? edits.get(turnId) : null;

        if (updatedText) {
          didUpdate = true;
          return { ...turn, Text: updatedText };
        }

        return turn;
      });

      if (!didUpdate) return message;

      return { ...message, content: JSON.stringify(nextTurns) };
    });
  };

  // ────────────────────────────────────────────────────────────────────────
  // Helper: user message index parsing and lookup
  // ────────────────────────────────────────────────────────────────────────

  /**
   * Parses a zero-based user message index from request input.
   *
   * @param value - Input value from the request body.
   * @returns The parsed index or null when invalid.
   */
  const parseUserMessageIndex = (value: unknown) => {
    const parsed = typeof value === "number" ? value : Number.parseInt(String(value ?? ""), 10);
    if (!Number.isInteger(parsed) || parsed < 0) return null;
    return parsed;
  };

  /**
   * Finds the history index for the Nth user message.
   *
   * @param history - Full chat history for the session.
   * @param userIndex - Zero-based user message index.
   * @returns Index within the history array or -1 when missing.
   */
  const findUserHistoryIndex = (history: { role: string }[], userIndex: number) => {
    let count = 0;
    for (let i = 0; i < history.length; i += 1) {
      if (history[i].role !== "user") continue;

      if (count === userIndex) return i;
      count += 1;
    }
    return -1;
  };

  // ────────────────────────────────────────────────────────────────────────
  // Helper: filter psychologist entries from reply for client response
  // ────────────────────────────────────────────────────────────────────────

  /**
   * Removes __PsychologistEval entries from the reply JSON for client consumption.
   * The full reply (with psychologist) is still stored in chat history.
   *
   * @param reply - Full assistant reply JSON string.
   * @returns Filtered JSON string without psychologist entries.
   */
  const filterPsychologistFromReply = (reply: string): string => {
    const turns = parseAssistantReply(reply);
    if (!turns.length) return reply;

    const filtered = turns.filter((turn) => {
      const name = typeof turn.CharacterName === "string" ? turn.CharacterName.trim() : "";
      return name !== PSYCHOLOGIST_CHARACTER;
    });

    if (filtered.length === turns.length) return reply;
    return JSON.stringify(filtered);
  };

  // ────────────────────────────────────────────────────────────────────────
  // Helper: build system prompt for diary chat
  // ────────────────────────────────────────────────────────────────────────

  /**
   * Builds the system prompt for a diary chat session.
   * Loads user profile, today's diary entries, and due review entries from DB.
   *
   * @param userId - Authenticated user ID.
   * @param body - Request body (unused currently, reserved for future params).
   * @returns The system prompt string.
   */
  const buildSystemPrompt = async (userId: number, body: unknown) => {
    if (deps.systemPromptBuilder) {
      const prompt = await deps.systemPromptBuilder({ userId, body });
      return (prompt ?? "").trim();
    }

    const user = await userRepository.findOne({
      where: { id: userId },
      relations: { level: true }
    });

    const { todayStart, todayEnd } = getTodayBounds();

    // Load today's diary entries
    const todayLogs = await myLogRepository.find({
      where: {
        userId,
        createdAt: Between(todayStart, todayEnd)
      },
      order: { createdAt: "DESC" }
    });

    // Load past entries due for review
    const now = new Date();
    const dueLogs = await myLogRepository.find({
      where: {
        userId,
        isArchived: false,
        nextReviewDate: LessThanOrEqual(now)
      },
      order: { createdAt: "DESC" }
    });

    // Filter out today's logs from due logs to avoid duplication
    const todayLogIds = new Set(todayLogs.map((log) => log.id));
    const filteredDueLogs = dueLogs.filter((log) => !todayLogIds.has(log.id));

    // Load active character names for the user
    const characters = await characterRepository.find({ where: { userId } });
    const characterNames = characters.map((c) => c.name).filter((n) => n.trim());

    return buildMyLogSystemPrompt({
      userName: user?.name ?? null,
      userAge: user?.age ?? null,
      userDescription: user?.description ?? null,
      level: (user as unknown as { level?: { level?: string } })?.level?.level ?? null,
      levelMaxWords: (user as unknown as { level?: { maxWords?: number } })?.level?.maxWords ?? null,
      levelGuideline: (user as unknown as { level?: { guideline?: string } })?.level?.guideline ?? null,
      levelDescription: (user as unknown as { level?: { descript?: string } })?.level?.descript ?? null,
      todayLogs: todayLogs.map((log) => ({
        id: log.id,
        content: log.content,
        createdAt: log.createdAt.toISOString()
      })),
      dueLogs: filteredDueLogs.map((log) => ({
        id: log.id,
        content: log.content,
        createdAt: log.createdAt.toISOString(),
        reviewCount: log.reviewCount
      })),
      characterNames
    });
  };

  // ────────────────────────────────────────────────────────────────────────
  // Helper: build message entities from history for journal persistence
  // ────────────────────────────────────────────────────────────────────────

  /**
   * Converts chat history messages into MyLogMessageEntity-compatible objects.
   * Marks psychologist evaluation entries with isHidden = true.
   *
   * @param history - Chat history messages.
   * @param userId - User ID.
   * @param journalId - Journal ID to link messages to.
   * @param voiceByCharacter - Map of character name → voice name for audio ID gen.
   * @returns Array of message entity data objects.
   */
  const buildMessageEntities = (
    history: ChatHistoryMessage[],
    userId: number,
    journalId: number,
    voiceByCharacter: Map<string, string>
  ) => {
    const result: Array<{
      content: string;
      characterName: string;
      translation: string | null;
      tone: string | null;
      audio: string | null;
      isHidden: boolean;
      userId: number;
      journalId: number;
    }> = [];

    for (const message of history) {
      if (message.role === "user") {
        const content = message.content.trim();
        if (!content) continue;

        result.push({
          content,
          characterName: "User",
          translation: null,
          tone: null,
          audio: null,
          isHidden: false,
          userId,
          journalId
        });
        continue;
      }

      if (message.role !== "assistant") continue;

      const turns = parseAssistantReply(message.content);
      if (!turns.length) {
        const fallback = message.content.trim();
        if (!fallback) continue;
        result.push({
          content: fallback,
          characterName: "Mimi",
          translation: null,
          tone: null,
          audio: null,
          isHidden: false,
          userId,
          journalId
        });
        continue;
      }

      for (const turn of turns) {
        const content = typeof turn.Text === "string" ? turn.Text.trim() : "";
        if (!content) continue;

        const characterName = typeof turn.CharacterName === "string" ? turn.CharacterName.trim() : "Mimi";
        const isPsychologist = characterName === PSYCHOLOGIST_CHARACTER;
        const translation = typeof turn.Translation === "string" ? turn.Translation.trim() : "";
        const tone = typeof turn.Tone === "string" ? turn.Tone.trim() : "";
        const voiceKey = normalizeName(characterName || "Mimi");
        const voiceName = voiceByCharacter.get(voiceKey) ?? "";
        const audio = !isPsychologist && tone ? buildAudioId(content, tone, voiceName || undefined) : null;

        result.push({
          content,
          characterName: characterName || "Mimi",
          translation: translation || null,
          tone: tone || null,
          audio,
          isHidden: isPsychologist,
          userId,
          journalId
        });
      }
    }

    return result;
  };

  // ====================================================================
  // HANDLER: Create a new diary entry
  // ====================================================================

  /**
   * Creates a new diary/log entry for the authenticated user.
   * Automatically sets the first review date to tomorrow.
   *
   * POST /api/mylog
   * Body: { content: string }
   */
  const createLog: MyLogController["createLog"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const content = typeof request.body?.content === "string" ? request.body.content.trim() : "";

    if (!content) {
      response.status(400).json({ message: "Diary content is required" });
      return;
    }

    try {
      const now = new Date();
      const nextReviewDate = createInitialReviewDate(now);

      const log = myLogRepository.create({
        content,
        userId: request.user.id,
        nextReviewDate,
        reviewCount: 0,
        isArchived: false
      });

      const saved = await myLogRepository.save(log);

      response.status(201).json({
        id: saved.id,
        content: saved.content,
        nextReviewDate: saved.nextReviewDate?.toISOString() ?? null,
        reviewCount: saved.reviewCount,
        isArchived: saved.isArchived,
        createdAt: saved.createdAt.toISOString()
      });
    } catch (error) {
      console.error("Error in createLog:", error);
      response.status(500).json({
        message: "Failed to create diary entry",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  // ====================================================================
  // HANDLER: List diary entries
  // ====================================================================

  /**
   * Lists all diary entries for the authenticated user, newest first.
   *
   * GET /api/mylog
   * Query: page (default 1), limit (default 20, max 100)
   */
  const listLogs: MyLogController["listLogs"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const page = Math.max(1, Number(request.query?.page ?? 1));
    const limit = Math.min(100, Math.max(1, Number(request.query?.limit ?? 20)));
    const skip = (page - 1) * limit;

    try {
      const [logs, total] = await myLogRepository.findAndCount({
        where: { userId: request.user.id },
        order: { createdAt: "DESC" },
        skip,
        take: limit
      });

      response.json({
        logs: logs.map((log) => ({
          id: log.id,
          content: log.content,
          nextReviewDate: log.nextReviewDate?.toISOString() ?? null,
          reviewCount: log.reviewCount,
          isArchived: log.isArchived,
          createdAt: log.createdAt.toISOString()
        })),
        total,
        page,
        limit,
        hasMore: skip + logs.length < total
      });
    } catch (error) {
      console.error("Error in listLogs:", error);
      response.status(500).json({
        message: "Failed to list diary entries",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  // ====================================================================
  // HANDLER: Update a diary entry
  // ====================================================================

  /**
   * Updates one diary entry content by ID for the authenticated user.
   *
   * PUT /api/mylog/:id
   * Body: { content: string }
   */
  const updateLog: MyLogController["updateLog"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const logId = Number(request.params?.id);
    if (!Number.isInteger(logId) || logId <= 0) {
      response.status(400).json({ message: "Invalid diary entry id" });
      return;
    }

    const content = typeof request.body?.content === "string" ? request.body.content.trim() : "";
    if (!content) {
      response.status(400).json({ message: "Diary content is required" });
      return;
    }

    try {
      const log = await myLogRepository.findOne({
        where: { id: logId, userId: request.user.id }
      });

      if (!log) {
        response.status(404).json({ message: "Diary entry not found" });
        return;
      }

      log.content = content;
      const saved = await myLogRepository.save(log);

      response.json({
        id: saved.id,
        content: saved.content,
        nextReviewDate: saved.nextReviewDate?.toISOString() ?? null,
        reviewCount: saved.reviewCount,
        isArchived: saved.isArchived,
        createdAt: saved.createdAt.toISOString()
      });
    } catch (error) {
      console.error("Error in updateLog:", error);
      response.status(500).json({
        message: "Failed to update diary entry",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  // ====================================================================
  // HANDLER: Get a single diary entry
  // ====================================================================

  /**
   * Returns a single diary entry by ID.
   *
   * GET /api/mylog/:id
   */
  const getLog: MyLogController["getLog"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const logId = Number(request.params?.id);

    if (!Number.isInteger(logId) || logId <= 0) {
      response.status(400).json({ message: "Invalid diary entry id" });
      return;
    }

    try {
      const log = await myLogRepository.findOne({
        where: { id: logId, userId: request.user.id }
      });

      if (!log) {
        response.status(404).json({ message: "Diary entry not found" });
        return;
      }

      response.json({
        id: log.id,
        content: log.content,
        nextReviewDate: log.nextReviewDate?.toISOString() ?? null,
        reviewCount: log.reviewCount,
        isArchived: log.isArchived,
        createdAt: log.createdAt.toISOString()
      });
    } catch (error) {
      console.error("Error in getLog:", error);
      response.status(500).json({
        message: "Failed to load diary entry",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  // ====================================================================
  // HANDLER: Get diary entries due for review
  // ====================================================================

  /**
   * Returns diary entries that are due for spaced-repetition review.
   * An entry is due when nextReviewDate <= now and isArchived = false.
   *
   * GET /api/mylog/review/due
   */
  const getDueLogs: MyLogController["getDueLogs"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const now = new Date();
      const dueLogs = await myLogRepository.find({
        where: {
          userId: request.user.id,
          isArchived: false,
          nextReviewDate: LessThanOrEqual(now)
        },
        order: { nextReviewDate: "ASC" }
      });

      response.json({
        logs: dueLogs.map((log) => ({
          id: log.id,
          content: log.content,
          nextReviewDate: log.nextReviewDate?.toISOString() ?? null,
          reviewCount: log.reviewCount,
          createdAt: log.createdAt.toISOString()
        })),
        total: dueLogs.length
      });
    } catch (error) {
      console.error("Error in getDueLogs:", error);
      response.status(500).json({
        message: "Failed to load due diary entries",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  // ====================================================================
  // HANDLER: Submit a review for a diary entry
  // ====================================================================

  /**
   * Marks a diary entry as reviewed, advancing its review count and
   * calculating the next review date. Archives the entry when all
   * scheduled reviews are complete.
   *
   * POST /api/mylog/review
   * Body: { logId: number }
   */
  const submitReview: MyLogController["submitReview"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const logId = typeof request.body?.logId === "number"
      ? request.body.logId
      : Number.parseInt(String(request.body?.logId ?? ""), 10);

    if (!Number.isInteger(logId) || logId <= 0) {
      response.status(400).json({ message: "Valid logId is required" });
      return;
    }

    try {
      const log = await myLogRepository.findOne({
        where: { id: logId, userId: request.user.id }
      });

      if (!log) {
        response.status(404).json({ message: "Diary entry not found" });
        return;
      }

      if (log.isArchived) {
        response.status(400).json({ message: "Diary entry is already archived" });
        return;
      }

      const now = new Date();
      const newReviewCount = log.reviewCount + 1;
      const nextReviewDate = calculateNextReviewDate(newReviewCount, now);
      const isArchived = nextReviewDate === null;

      log.reviewCount = newReviewCount;
      log.nextReviewDate = nextReviewDate;
      log.isArchived = isArchived;

      await myLogRepository.save(log);

      response.json({
        id: log.id,
        reviewCount: log.reviewCount,
        nextReviewDate: log.nextReviewDate?.toISOString() ?? null,
        isArchived: log.isArchived
      });
    } catch (error) {
      console.error("Error in submitReview:", error);
      response.status(500).json({
        message: "Failed to submit review",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  // ====================================================================
  // HANDLER: Send a chat message in diary chat
  // ====================================================================

  /**
   * Sends a user message in the diary chat session.
   * The AI responds based on today's diary + due review entries.
   * The __PsychologistEval entry is stored in history but filtered from client response.
   *
   * POST /api/mylog/chat/send
   * Body: { message: string }
   */
  const sendMessage: MyLogController["sendMessage"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const message = typeof request.body?.message === "string" ? request.body.message.trim() : "";

    if (!message) {
      response.status(400).json({ message: "Message is required" });
      return;
    }

    // Determine which AI service to use based on configured model
    const useGemini = isGeminiModel(mylogModel);
    const selectedService = useGemini ? geminiService : openAIService;
    const serviceName = useGemini ? "Gemini" : "OpenAI";

    if (!selectedService) {
      response.status(500).json({ message: `${serviceName} API key is not configured` });
      return;
    }

    try {
      const systemPrompt = await buildSystemPrompt(request.user.id, request.body);
      await historyStore.ensureSystemMessage(request.user.id, systemPrompt);
      const history = await historyStore.load(request.user.id);

      const result = await requestJsonReplyWithRetry(
        selectedService,
        message,
        history,
        mylogModel
      );

      // Normalize message IDs to prevent duplicates
      const normalizedReply = useGemini
        ? normalizeAssistantReplyMessageIds(result.reply, collectAssistantMessageIds(history))
        : result.reply;

      // Store FULL reply (including psychologist eval) in history for AI context
      await historyStore.append(request.user.id, [
        { role: "user", content: message },
        { role: "assistant", content: normalizedReply }
      ]);

      // Filter out psychologist eval from client response
      const clientReply = filterPsychologistFromReply(normalizedReply);

      response.json({
        reply: clientReply,
        model: result.model
      });
    } catch (error) {
      console.error("Error in MyLog sendMessage:", error);
      response.status(500).json({
        message: "Failed to generate reply",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  // ====================================================================
  // HANDLER: Get diary chat history
  // ====================================================================

  /**
   * Returns the current diary chat history, filtering out system/developer
   * messages and psychologist evaluation entries.
   *
   * GET /api/mylog/chat/history
   */
  const getHistory: MyLogController["getHistory"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const messages = await historyStore.load(request.user.id);

      // Apply assistant edits from developer notes before filtering
      const adjustedMessages = applyAssistantEdits(messages);

      // Filter out system/developer messages
      const filtered = adjustedMessages.filter((msg) => msg.role !== "system" && msg.role !== "developer");

      // For assistant messages, remove psychologist entries from the content
      const clientMessages = filtered.map((msg) => {
        if (msg.role === "assistant") {
          return { ...msg, content: filterPsychologistFromReply(msg.content) };
        }
        return msg;
      });

      response.json({ messages: clientMessages });
    } catch (error) {
      console.error("Error in MyLog getHistory:", error);
      response.status(500).json({
        message: "Failed to load diary chat history",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  // ====================================================================
  // HANDLER: End diary chat conversation
  // ====================================================================

  /**
   * Ends the current diary chat session:
   * 1. Asks AI for a summary of the conversation
   * 2. Creates a MyLogJournal with the summary
   * 3. Persists all messages (including hidden psychologist entries)
   * 4. Auto-advances review for diary entries that were discussed
   * 5. Clears the chat history file
   *
   * POST /api/mylog/chat/end
   */
  const endConversation: MyLogController["endConversation"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    // Use Gemini or OpenAI for summary based on configured model
    const useGemini = isGeminiModel(mylogModel);
    const summaryService = useGemini ? geminiService : openAIService;

    if (!summaryService) {
      response.status(500).json({ message: "AI service is not configured" });
      return;
    }

    try {
      const history = await historyStore.load(request.user.id);
      const hasConversation = history.some((msg) => msg.role === "user" || msg.role === "assistant");

      if (!hasConversation) {
        response.status(400).json({ message: "No conversation history to summarize" });
        return;
      }

      // Request summary from AI
      const summaryInstruction = `
Please summarize this diary chat conversation and return it in JSON format:
{
  "Summary": "Vietnamese summary of the diary chat session — what topics were discussed, what emotions were explored.",
  "EmotionalSummary": "English: overall emotional arc — starting emotion, emotional shifts during conversation, and final emotional state."
}
Return ONLY the JSON object. No markdown. No extra text.
`.trim();

      const summaryReply = await summaryService.createReply(undefined, [
        ...history,
        { role: "developer", content: summaryInstruction }
      ], mylogModel);

      let summary = "Diary chat session.";
      let emotionalSummary: string | null = null;

      try {
        const parsed = JSON.parse(summaryReply.reply) as { Summary?: string; EmotionalSummary?: string };
        summary = typeof parsed.Summary === "string" ? parsed.Summary.trim() : summary;
        emotionalSummary = typeof parsed.EmotionalSummary === "string" ? parsed.EmotionalSummary.trim() : null;
      } catch {
        // Use raw reply as summary if JSON parse fails
        summary = summaryReply.reply.trim() || summary;
      }

      // Create journal entry
      const journal = myLogJournalRepository.create({
        summary,
        emotionalSummary,
        userId: request.user.id
      });
      const savedJournal = await myLogJournalRepository.save(journal);

      // Build and save message entities
      const characters = await characterRepository.find({ where: { userId: request.user.id } });
      const voiceByCharacter = new Map(
        characters
          .filter((c) => c.voiceName)
          .map((c) => [normalizeName(c.name), c.voiceName as string])
      );

      const messageEntities = buildMessageEntities(history, request.user.id, savedJournal.id, voiceByCharacter);

      if (messageEntities.length) {
        await myLogMessageRepository.save(
          messageEntities.map((msg) => myLogMessageRepository.create(msg))
        );
      }

      // Auto-advance review for due diary entries
      const now = new Date();
      const dueLogs = await myLogRepository.find({
        where: {
          userId: request.user.id,
          isArchived: false,
          nextReviewDate: LessThanOrEqual(now)
        }
      });

      for (const log of dueLogs) {
        const newReviewCount = log.reviewCount + 1;
        const nextReviewDate = calculateNextReviewDate(newReviewCount, now);
        log.reviewCount = newReviewCount;
        log.nextReviewDate = nextReviewDate;
        log.isArchived = nextReviewDate === null;
      }

      if (dueLogs.length) {
        await myLogRepository.save(dueLogs);
      }

      // Clear chat history
      await historyStore.clear(request.user.id);

      response.json({
        journalId: savedJournal.id,
        summary: savedJournal.summary,
        emotionalSummary: savedJournal.emotionalSummary ?? null,
        reviewsAdvanced: dueLogs.length
      });
    } catch (error) {
      console.error("Error in MyLog endConversation:", error);
      response.status(500).json({
        message: "Failed to finalize diary journal",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  // ====================================================================
  // HANDLER: List diary journals (summaries of past chat sessions)
  // ====================================================================

  /**
   * Lists all diary chat journals for the authenticated user.
   *
   * GET /api/mylog/journals
   */
  const listJournals: MyLogController["listJournals"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const journals = await myLogJournalRepository.find({
        where: { userId: request.user.id },
        order: { createdAt: "DESC" }
      });

      response.json({
        journals: journals.map((j) => ({
          id: j.id,
          summary: j.summary,
          emotionalSummary: j.emotionalSummary ?? null,
          createdAt: j.createdAt.toISOString()
        }))
      });
    } catch (error) {
      console.error("Error in MyLog listJournals:", error);
      response.status(500).json({
        message: "Failed to load diary journals",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  // ====================================================================
  // HANDLER: Get a diary journal with messages
  // ====================================================================

  /**
   * Returns a single diary journal with its messages.
   * Hidden psychologist messages are excluded from the response.
   *
   * GET /api/mylog/journals/:id
   */
  const getJournal: MyLogController["getJournal"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const journalId = Number(request.params?.id);

    if (!Number.isInteger(journalId) || journalId <= 0) {
      response.status(400).json({ message: "Invalid journal id" });
      return;
    }

    try {
      const journal = await myLogJournalRepository.findOne({
        where: { id: journalId, userId: request.user.id }
      });

      if (!journal) {
        response.status(404).json({ message: "Journal not found" });
        return;
      }

      const messages = await myLogMessageRepository.find({
        where: {
          journalId: journal.id,
          userId: request.user.id,
          isHidden: false
        },
        order: { createdAt: "ASC" }
      });

      response.json({
        journal: {
          id: journal.id,
          summary: journal.summary,
          emotionalSummary: journal.emotionalSummary ?? null,
          createdAt: journal.createdAt.toISOString()
        },
        messages: messages.map((msg) => ({
          id: msg.id,
          content: msg.content,
          characterName: msg.characterName,
          translation: msg.translation,
          tone: msg.tone,
          audio: msg.audio,
          createdAt: msg.createdAt.toISOString()
        }))
      });
    } catch (error) {
      console.error("Error in MyLog getJournal:", error);
      response.status(500).json({
        message: "Failed to load diary journal",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  // ====================================================================
  // HANDLER: Append a developer message to diary chat
  // ====================================================================

  /**
   * Appends a developer-role message to the diary chat history.
   * Supports character_added, character_removed, and context_update kinds.
   *
   * POST /api/mylog/chat/developer
   * Body: { kind: string, character?: object, context?: string }
   */
  const appendDeveloperMessage: MyLogController["appendDeveloperMessage"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const payload = (request.body ?? {}) as Record<string, unknown>;
    const kind = typeof payload.kind === "string" ? payload.kind.trim() : "";

    if (kind !== "character_added" && kind !== "character_removed" && kind !== "context_update") {
      response.status(400).json({ message: "Invalid developer message kind" });
      return;
    }

    const content =
      kind === "character_added"
        ? formatCharacterAddedMessage(payload)
        : kind === "character_removed"
        ? formatCharacterRemovedMessage(payload)
        : formatContextMessage(payload);

    if (!content) {
      const message = kind === "context_update" ? "Context is required" : "Character name is required";
      response.status(400).json({ message });
      return;
    }

    try {
      await historyStore.append(request.user.id, [{ role: "developer", content }]);
      response.json({ ok: true });
    } catch (error) {
      console.error("Error in MyLog appendDeveloperMessage:", error);
      response.status(500).json({
        message: "Failed to append developer message",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  // ====================================================================
  // HANDLER: Edit a user or assistant message in diary chat
  // ====================================================================

  /**
   * Applies edits to a user or assistant message in the diary chat history.
   *
   * For assistant edits: appends a developer note with the new content.
   * For user edits: truncates history at the target message, replaces it,
   * and re-generates the AI reply.
   *
   * POST /api/mylog/chat/edit
   * Body: { kind: "user"|"assistant", ... }
   */
  const editMessage: MyLogController["editMessage"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const payload = (request.body ?? {}) as Record<string, unknown>;
    const kind = typeof payload.kind === "string" ? payload.kind.trim() : "";

    if (kind !== "user" && kind !== "assistant") {
      response.status(400).json({ message: "Invalid edit kind" });
      return;
    }

    // --- Assistant message edit: append developer note ---
    if (kind === "assistant") {
      const messageId = typeof payload.assistantMessageId === "string" ? payload.assistantMessageId.trim() : "";
      const editedContent = typeof payload.content === "string" ? payload.content.trim() : "";
      const content = formatAssistantEditMessage(messageId, editedContent);

      if (!messageId) {
        response.status(400).json({ message: "Assistant messageId is required" });
        return;
      }

      if (!editedContent) {
        response.status(400).json({ message: "Edited content is required" });
        return;
      }

      if (!content) {
        response.status(400).json({ message: "Edited content is required" });
        return;
      }

      try {
        await historyStore.append(request.user.id, [{ role: "developer", content }]);
        response.json({ ok: true });
      } catch (error) {
        console.error("Error in MyLog editMessage (assistant):", error);
        response.status(500).json({
          message: "Failed to append developer message",
          error: error instanceof Error ? error.message : "Unknown error"
        });
      }

      return;
    }

    // --- User message edit: truncate + re-generate ---
    const editedContent = typeof payload.content === "string" ? payload.content.trim() : "";
    const userIndex = parseUserMessageIndex(payload.userMessageIndex);

    if (!editedContent) {
      response.status(400).json({ message: "Edited content is required" });
      return;
    }

    if (userIndex === null) {
      response.status(400).json({ message: "User message index is required" });
      return;
    }

    // Determine which AI service to use
    const useGemini = isGeminiModel(mylogModel);
    const selectedService = useGemini ? geminiService : openAIService;
    const serviceName = useGemini ? "Gemini" : "OpenAI";

    if (!selectedService) {
      response.status(500).json({ message: `${serviceName} API key is not configured` });
      return;
    }

    try {
      const history = await historyStore.load(request.user.id);
      const targetIndex = findUserHistoryIndex(history, userIndex);

      if (targetIndex < 0) {
        response.status(404).json({ message: "User message not found" });
        return;
      }

      const prefix = history.slice(0, targetIndex);
      const systemPrompt = await buildSystemPrompt(request.user.id, request.body);
      const prefixWithoutSystem = prefix.filter((message) => message.role !== "system");
      const historyForAI: ChatHistoryMessage[] = [
        { role: "system", content: systemPrompt },
        ...prefixWithoutSystem
      ];

      await historyStore.clear(request.user.id);
      await historyStore.ensureSystemMessage(request.user.id, systemPrompt);

      const result = await requestJsonReplyWithRetry(
        selectedService,
        editedContent,
        historyForAI,
        mylogModel
      );

      const normalizedReply = useGemini
        ? normalizeAssistantReplyMessageIds(result.reply, collectAssistantMessageIds(prefixWithoutSystem))
        : result.reply;
      const nextMessages: ChatHistoryMessage[] = [
        ...prefixWithoutSystem,
        { role: "user", content: editedContent },
        { role: "assistant", content: normalizedReply }
      ];

      await historyStore.append(request.user.id, nextMessages);

      // Filter out psychologist eval and system/developer messages for client
      const clientMessages = nextMessages
        .filter((message) => message.role !== "system" && message.role !== "developer")
        .map((msg) => {
          if (msg.role === "assistant") {
            return { ...msg, content: filterPsychologistFromReply(msg.content) };
          }
          return msg;
        });

      const clientReply = filterPsychologistFromReply(normalizedReply);

      response.json({
        messages: clientMessages,
        reply: clientReply,
        model: result.model
      });
    } catch (error) {
      console.error("Error in MyLog editMessage (user):", error);
      response.status(500).json({
        message: "Failed to edit chat message",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  // ====================================================================
  // HANDLER: Get developer state for diary chat
  // ====================================================================

  /**
   * Returns the list of currently active character names based on
   * developer messages in the diary chat history.
   *
   * GET /api/mylog/chat/developer-state
   */
  const getDeveloperState: MyLogController["getDeveloperState"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const messages = await historyStore.load(request.user.id);
      const activeMap = new Map<string, boolean>();

      for (const message of messages) {
        if (message.role !== "developer") continue;

        const action = parseDeveloperCharacterAction(message.content);
        if (action?.name) {
          activeMap.set(action.name, action.active);
        }
      }

      const activeCharacterNames = Array.from(activeMap.entries())
        .filter(([, isActive]) => isActive)
        .map(([name]) => name);

      response.json({ activeCharacterNames });
    } catch (error) {
      console.error("Error in MyLog getDeveloperState:", error);
      response.status(500).json({
        message: "Failed to load developer state",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return {
    createLog,
    listLogs,
    updateLog,
    getLog,
    getDueLogs,
    submitReview,
    sendMessage,
    getHistory,
    endConversation,
    listJournals,
    getJournal,
    appendDeveloperMessage,
    editMessage,
    getDeveloperState
  };
};
