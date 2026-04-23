import type { Request, Response } from "express";
import { randomUUID } from "crypto";
import type { DataSource } from "typeorm";
import { toFile, type Uploadable } from "openai/uploads";
import { createOpenAIChatService, createOpenAIClient, type OpenAIChatService } from "../../services/openai.service.js";
import { createGeminiChatService, isGeminiModel, type GeminiChatService, type GeminiAudioPart } from "../../services/gemini.service.js";
import { buildChatSystemPrompt } from "../../services/chat-prompt.service.js";
import StoryEntity from "../../models/story.entity.js";
import UserEntity from "../../models/user.entity.js";
import CharacterEntity from "../../models/character.entity.js";
import CharacterRelationshipEntity from "../../models/character-relationship.entity.js";
import { createChatHistoryStore, type ChatHistoryMessage, type ChatHistoryStore } from "../../services/chat-history.service.js";
import { createVectorMemoryService, type VectorMemoryService } from "../../services/vector-memory.service.js";
import { extractMemorySidecars, shouldStoreMemory, buildMemoryItemFromCandidate, stripMemorySidecar } from "../../services/memory-extraction.service.js";
import { createMemoryRetrievalService, type MemoryRetrievalService } from "../../services/memory-retrieval.service.js";
import { createCheapAIService } from "../../services/cheap-ai.service.js";

interface ChatController {
  sendMessage: (request: Request, response: Response) => Promise<void>;
  respondFromHistory: (request: Request, response: Response) => Promise<void>;
  getHistory: (request: Request, response: Response) => Promise<void>;
  appendDeveloperMessage: (request: Request, response: Response) => Promise<void>;
  editMessage: (request: Request, response: Response) => Promise<void>;
  getDeveloperState: (request: Request, response: Response) => Promise<void>;
  transcribeAudio: (request: Request, response: Response) => Promise<void>;
  /** Returns system prompt + history for local AI clients (PC Ollama). */
  prepareLocalPrompt: (request: Request, response: Response) => Promise<void>;
  /** Saves user message + locally-generated AI reply to history. */
  saveLocalReply: (request: Request, response: Response) => Promise<void>;
}

interface ChatControllerDeps {
  openAIService?: OpenAIChatService;
  geminiService?: GeminiChatService;
  historyStore?: ChatHistoryStore;
  transcribeWithOpenAI?: (file: Uploadable, language?: string) => Promise<string>;
  /**
   * Optional override for building the system instruction text.
   * Primarily used by unit tests to avoid database lookups.
   */
  systemPromptBuilder?: (params: { userId: number; body: unknown }) => Promise<string> | string;
  /** Optional vector memory service for long-term memory storage. */
  vectorMemoryService?: VectorMemoryService;
  /** Optional memory retrieval service for recalling long-term memories. */
  memoryRetrievalService?: MemoryRetrievalService;
}

interface AssistantTurn {
  MessageId?: string;
  CharacterName?: string;
  Text?: string;
  Pinyin?: string;
  Tone?: string;
  Translation?: string;
  Transcribe?: string;
  /** Sidecar: global/objective memory (first item only). */
  GlobalMemoryEn?: string;
  GlobalMemoryType?: string;
  GlobalMemoryImportance?: string;
  /** Sidecar: character subjective memory (any item, actor = CharacterName). */
  ImportantMemoryEn?: string;
  ImportantMemoryType?: string;
  ImportantMemoryImportance?: string;
  ImportantMemoryActor?: string;
  [key: string]: unknown;
}

type ChatReplyService = OpenAIChatService | GeminiChatService;

interface JsonReplyResult {
  reply: string;
  model: string;
}

const MAX_AUDIO_BYTES = 12 * 1024 * 1024;
const DEFAULT_TRANSCRIBE_LANGUAGE = "zh";

/**
 * Parses a base64 audio data URL and extracts mime type + binary buffer.
 */
const parseAudioDataUrl = (dataUrl: string) => {
  const match = /^data:(audio\/[a-z0-9.+-]+)(?:;[^,]*)?;base64,(.+)$/i.exec(dataUrl.trim());

  if (!match) {
    return null;
  }

  const mime = match[1];
  const buffer = Buffer.from(match[2], "base64");

  return { mime, buffer };
};

/**
 * Resolves file extension from audio mime type.
 */
const resolveAudioExtension = (mime: string) => {
  if (mime.includes("webm")) return "webm";
  if (mime.includes("wav")) return "wav";
  if (mime.includes("mpeg") || mime.includes("mp3")) return "mp3";
  if (mime.includes("ogg")) return "ogg";
  return "webm";
};

/**
 * Builds the Chat controller with injected data source dependencies.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @param deps - Optional overrides for external services.
 * @returns The Chat controller handlers.
 */
export const createChatController = (
  _dataSource: DataSource,
  deps: ChatControllerDeps = {}
): ChatController => {
  const dataSource = _dataSource;
  const userRepository = dataSource.getRepository(UserEntity);
  const storyRepository = dataSource.getRepository(StoryEntity);
  const characterRepository = dataSource.getRepository(CharacterEntity);
  const relationshipRepository = dataSource.getRepository(CharacterRelationshipEntity);

  // OpenAI configuration
  const openAIApiKey = process.env.OPENAI_API_KEY ?? "";
  const openAIModel = process.env.OPENAI_MODEL ?? "gpt-4.1-mini";
  const systemPromptPath = process.env.OPENAI_SYSTEM_PROMPT_PATH;
  const openAIClient = openAIApiKey ? createOpenAIClient(openAIApiKey) : null;
  const openAIService =
    deps.openAIService ?? (openAIApiKey ? createOpenAIChatService({ apiKey: openAIApiKey, model: openAIModel, systemPromptPath }) : null);

  // Gemini configuration
  const geminiApiKey = process.env.GOOGLE_API_KEY ?? "";
  const geminiModel = process.env.GEMINI_MODEL ?? "gemini-2.5-flash";
  const geminiService =
    deps.geminiService ?? (geminiApiKey ? createGeminiChatService({ apiKey: geminiApiKey, model: geminiModel }) : null);

  const historyStore = deps.historyStore ?? createChatHistoryStore();

  // Long-term memory services (active only when CHROMA_URL is set)
  const chromaUrl = process.env.CHROMA_URL ?? "";
  let vectorMemoryService: VectorMemoryService | undefined = deps.vectorMemoryService;
  let memoryRetrievalService: MemoryRetrievalService | undefined = deps.memoryRetrievalService;

  if (chromaUrl && !vectorMemoryService) {
    try {
      vectorMemoryService = createVectorMemoryService({ chromaUrl });
      const cheapAI = createCheapAIService();
      memoryRetrievalService = createMemoryRetrievalService({ vectorMemory: vectorMemoryService, cheapAI });
      console.log("Long-term memory enabled (ChromaDB at", chromaUrl, ")");
    } catch (error) {
      console.warn("Failed to initialise long-term memory services:", error);
      vectorMemoryService = undefined;
      memoryRetrievalService = undefined;
    }
  }

  const transcribeWithOpenAI =
    deps.transcribeWithOpenAI ??
    (async (file, language) => {
      if (!openAIClient) {
        throw new Error("OpenAI API key is not configured");
      }

      const resolvedLanguage = typeof language === "string" ? language.trim() : "";
      const transcription = await openAIClient.audio.transcriptions.create({
        file,
        model: "gpt-4o-mini-transcribe",
        ...(resolvedLanguage ? { language: resolvedLanguage } : {})
      });

      const transcript = transcription.text?.trim() ?? "";
      if (!transcript) {
        throw new Error("OpenAI returned an empty transcript");
      }

      return transcript;
    });

  let hasLoggedAssistantReplyParseFailure = false;

  const getSessionId = (value: unknown) => (typeof value === "string" ? value.trim() : "");

  const getOptionalString = (value: unknown) => (typeof value === "string" ? value.trim() : "");

  /**
   * Parses a zero-based user message index.
   *
   * @param value - Input value from the request body.
   * @returns The parsed index or null when invalid.
   */
  const parseUserMessageIndex = (value: unknown) => {
    const parsed = typeof value === "number" ? value : Number.parseInt(String(value ?? ""), 10);
    if (!Number.isInteger(parsed) || parsed < 0) {
      return null;
    }
    return parsed;
  };

  /**
   * Loads the story used for prompt enrichment.
   * Always uses the authenticated user's currentStoryId.
   *
   * @param userId - Authenticated user id.
   * @returns The matching story or null when not found.
   */
  const loadStoryForPrompt = async (userId: number) => {
    const user = await userRepository.findOne({ where: { id: userId } });
    const storyId = user?.currentStoryId ?? null;

    if (!storyId) {
      return null;
    }

    return storyRepository.findOne({
      where: {
        id: storyId,
        userId
      }
    });
  };

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

    if (gender) {
      lines.push(`Gender: ${gender}`);
    }

    if (age != null) {
      lines.push(`Age: ${age}`);
    }

    if (personality) {
      lines.push(`Personality: ${personality}`);
    }

    if (appearance) {
      lines.push(`Appearance: ${appearance}`);
    }

    return lines.join("\n");
  };

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
   * Queries relationship records for the given active characters and formats
   * them into a structured text block suitable for the system prompt.
   *
   * @param userId - Authenticated user id.
   * @param activeCharacterNames - Names of currently active characters.
   * @returns Formatted relationship block or null when no relationships exist.
   */
  const buildRelationshipBlock = async (
    userId: number,
    activeCharacterNames: string[]
  ): Promise<string | null> => {
    if (activeCharacterNames.length === 0) return null;

    // Resolve character IDs from names
    const characters = await characterRepository
      .createQueryBuilder("c")
      .where("c.user_id = :userId", { userId })
      .andWhere("c.name IN (:...names)", { names: activeCharacterNames })
      .getMany();

    if (characters.length === 0) return null;

    const characterIds = characters.map(c => c.id);
    const nameById = new Map(characters.map(c => [c.id, c.name]));

    // Fetch all relationships owned by active characters for this user
    const relationships = await relationshipRepository
      .createQueryBuilder("r")
      .where("r.ownerCharacterId IN (:...ids)", { ids: characterIds })
      .andWhere("r.userId = :userId", { userId })
      .getMany();

    if (relationships.length === 0) return null;

    const lines: string[] = [];

    for (const rel of relationships) {
      const ownerName = nameById.get(rel.ownerCharacterId) ?? `Character#${rel.ownerCharacterId}`;
      const targetLabel = rel.targetType === "user"
        ? "User"
        : (nameById.get(rel.targetCharacterId ?? 0) ?? `Character#${rel.targetCharacterId}`);

      lines.push(`[${ownerName} → ${targetLabel}]`);
      lines.push(`Kind: ${rel.relationshipKind}`);
      lines.push(`Stable thought: ${rel.stableThought}`);
      if (rel.temporaryThought) {
        lines.push(`Temporary thought: ${rel.temporaryThought}`);
      }
      lines.push(`Stable emotion: ${rel.stableEmotion.toFixed(1)} / 10`);
      lines.push(`Current emotion: ${rel.currentEmotion.toFixed(1)} / 10`);
      if (rel.emotionCause) {
        lines.push(`Emotion cause: ${rel.emotionCause}`);
      }
      lines.push(""); // blank line separator
    }

    return lines.join("\n").trim() || null;
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
    if (!trimmed) {
      return "";
    }

    const updatedContent = content.trim();
    if (!updatedContent) {
      return "";
    }

    return [
      `Assistant message edited: ${trimmed}.`,
      "New content:",
      updatedContent
    ].join("\n");
  };

  const parseAssistantReply = (content: string): AssistantTurn[] => {
    const trimmed = content.trim();

    if (!trimmed) {
      return [];
    }

    const tryParse = (input: string) => {
      try {
        const parsed = JSON.parse(input) as unknown;
        if (Array.isArray(parsed)) {
          return parsed as AssistantTurn[];
        }
        if (parsed && typeof parsed === "object") {
          return [parsed as AssistantTurn];
        }
      } catch (error) {
        if (!hasLoggedAssistantReplyParseFailure) {
          console.warn("Failed to parse assistant reply as JSON; attempting fallback extraction.", error);
          hasLoggedAssistantReplyParseFailure = true;
        }
        return null;
      }

      return null;
    };

    const direct = tryParse(trimmed);
    if (direct) {
      return direct;
    }

    const arrayStart = trimmed.indexOf("[");
    const arrayEnd = trimmed.lastIndexOf("]");
    if (arrayStart !== -1 && arrayEnd > arrayStart) {
      const sliced = tryParse(trimmed.slice(arrayStart, arrayEnd + 1));
      if (sliced) {
        return sliced;
      }
    }

    const objectStart = trimmed.indexOf("{");
    const objectEnd = trimmed.lastIndexOf("}");
    if (objectStart !== -1 && objectEnd > objectStart) {
      const sliced = tryParse(trimmed.slice(objectStart, objectEnd + 1));
      if (sliced) {
        return sliced;
      }
    }

    return [];
  };

  /**
   * Validates that assistant turns match the required JSON schema shape.
   *
   * @param turns - Parsed assistant turns.
   * @returns True when all required fields exist and are non-empty strings.
   */
  const isValidAssistantTurnSchema = (turns: AssistantTurn[]) => {
    if (!Array.isArray(turns) || turns.length === 0) {
      return false;
    }

    for (const turn of turns) {
      if (!turn || typeof turn !== "object") {
        return false;
      }

      const messageId = typeof turn.MessageId === "string" ? turn.MessageId.trim() : "";
      const characterName = typeof turn.CharacterName === "string" ? turn.CharacterName.trim() : "";
      const text = typeof turn.Text === "string" ? turn.Text.trim() : "";
      const pinyin = typeof turn.Pinyin === "string" ? turn.Pinyin.trim() : "";
      const tone = typeof turn.Tone === "string" ? turn.Tone.trim() : "";
      const translation = typeof turn.Translation === "string" ? turn.Translation.trim() : "";

      if (!messageId || !characterName || !text || !pinyin || !tone || !translation) {
        return false;
      }
    }

    return true;
  };

  /**
   * Creates a corrective retry prompt when assistant output is not valid JSON schema.
   *
   * @param userMessage - Original user message.
   * @returns Retry prompt text.
   */
  const buildJsonRetryPrompt = (promptSeed: string) => {
    return [
      promptSeed,
      "",
      "Your previous response did not match the required JSON format.",
      "Retry now and return ONLY valid JSON array.",
      "Requirements:",
      "- Must be a JSON array (1-10 items).",
      "- Every item must include non-empty string fields: MessageId, CharacterName, Text, Pinyin, Tone, Translation.",
      "- No markdown, no explanations, no comments."
    ].join("\n");
  };

  /**
   * Creates a corrective retry prompt when the AI used invalid character names.
   *
   * @param promptSeed - Original user message seed for retry.
   * @param invalidNames - Character names that were not in the allowed list.
   * @param allowedNames - List of valid character names.
   * @returns Corrective retry prompt text.
   */
  const buildCharacterNameRetryPrompt = (promptSeed: string, invalidNames: string[], allowedNames: string[]) => {
    return [
      promptSeed,
      "",
      `Your previous response used invalid CharacterName values: ${invalidNames.map(n => `"${n}"`).join(", ")}.`,
      `ONLY the following CharacterName values are allowed: ${allowedNames.map(n => `"${n}"`).join(", ")}.`,
      "Retry now. Use ONLY allowed CharacterName values.",
      "Return ONLY valid JSON array with the correct CharacterName values."
    ].join("\n");
  };

  /**
   * Checks whether all CharacterName values in the parsed turns belong to the allowed set.
   *
   * @param turns - Parsed assistant turns.
   * @param allowedNames - Allowed character names (case-insensitive).
   * @returns Object with validation result and any invalid names found.
   */
  const validateCharacterNames = (turns: AssistantTurn[], allowedNames: string[]): { valid: boolean; invalidNames: string[] } => {
    if (allowedNames.length === 0) {
      return { valid: true, invalidNames: [] };
    }

    const allowedSet = new Set(allowedNames.map(n => n.trim().toLowerCase()));
    const invalidNames: string[] = [];

    for (const turn of turns) {
      const name = typeof turn.CharacterName === "string" ? turn.CharacterName.trim() : "";
      if (name && !allowedSet.has(name.toLowerCase())) {
        invalidNames.push(name);
      }
    }

    return { valid: invalidNames.length === 0, invalidNames };
  };

  /**
   * Requests a reply from AI and retries with a corrective prompt when JSON format is invalid
   * or when CharacterName values are not in the allowed set.
   *
   * @param service - Target chat service.
   * @param message - User message to send.
   * @param history - Message history.
   * @param modelOverride - Optional model override.
   * @param retryLimit - Max retry attempts after the initial call.
   * @param audioParts - Optional audio parts for Gemini.
   * @param sessionKey - Optional session key for Gemini caching.
   * @param allowedCharacterNames - Optional list of valid character names.
   * @returns JSON-valid assistant reply result.
   */
  const requestJsonReplyWithRetry = async (
    service: ChatReplyService,
    message: string | undefined,
    history: Array<{ role: "system" | "developer" | "user" | "assistant"; content: string }>,
    modelOverride?: string,
    retryLimit = 2,
    audioParts?: GeminiAudioPart[],
    sessionKey?: string,
    allowedCharacterNames?: string[]
  ): Promise<JsonReplyResult> => {
    let attempt = 0;
    let prompt = typeof message === "string" && message.trim() ? message.trim() : undefined;
    const retryPromptSeed = prompt || "Continue the conversation naturally based on the current chat history.";
    let lastResult: JsonReplyResult | null = null;
    const effectiveAllowedNames = (allowedCharacterNames ?? []).filter(n => n.trim());

    while (attempt <= retryLimit) {
      // Only send audio on the first attempt; retries use text-only corrective prompts
      const audioForAttempt = attempt === 0 ? audioParts : undefined;
      const result = await service.createReply(prompt, history, modelOverride || undefined, audioForAttempt, sessionKey);
      lastResult = result;

      const turns = parseAssistantReply(result.reply);
      if (!isValidAssistantTurnSchema(turns)) {
        attempt += 1;
        if (attempt > retryLimit) {
          break;
        }
        console.warn(`Assistant reply JSON invalid at attempt ${attempt}. Retrying with strict JSON format reminder.`);
        prompt = buildJsonRetryPrompt(retryPromptSeed);
        continue;
      }

      // Validate character names if allowed list is provided
      const { valid, invalidNames } = validateCharacterNames(turns, effectiveAllowedNames);
      if (valid) {
        return result;
      }

      attempt += 1;
      if (attempt > retryLimit) {
        console.warn(`[Chat] AI used invalid CharacterNames after all retries: ${invalidNames.join(", ")}. Returning response as-is.`);
        return result;
      }

      console.warn(`[Chat] AI used invalid CharacterNames: ${invalidNames.join(", ")}. Retrying (attempt ${attempt}).`);
      prompt = buildCharacterNameRetryPrompt(retryPromptSeed, invalidNames, effectiveAllowedNames);
    }

    throw new Error("AI returned invalid JSON format after retries");
  };

  const collectAssistantMessageIds = (messages: { role: string; content: string }[]) => {
    const ids = new Set<string>();

    for (const message of messages) {
      if (message.role !== "assistant") {
        continue;
      }

      const turns = parseAssistantReply(message.content);
      for (const turn of turns) {
        const messageId = typeof turn.MessageId === "string" ? turn.MessageId.trim() : "";
        if (!messageId) {
          continue;
        }

        ids.add(messageId);
      }
    }

    return ids;
  };

  const normalizeAssistantReplyMessageIds = (reply: string, usedIds: Set<string>) => {
    const turns = parseAssistantReply(reply);
    if (!turns.length) {
      return reply;
    }

    let hasChanged = false;
    const normalizedTurns = turns.map((turn) => {
      const currentId = typeof turn.MessageId === "string" ? turn.MessageId.trim() : "";
      const shouldReplaceId = !currentId || usedIds.has(currentId);

      if (!shouldReplaceId) {
        usedIds.add(currentId);
        return turn;
      }

      let nextId = randomUUID();
      while (usedIds.has(nextId)) {
        nextId = randomUUID();
      }

      usedIds.add(nextId);
      hasChanged = true;
      return { ...turn, MessageId: nextId };
    });

    if (!hasChanged) {
      return reply;
    }

    return JSON.stringify(normalizedTurns);
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
      if (history[i].role !== "user") {
        continue;
      }

      if (count === userIndex) {
        return i;
      }

      count += 1;
    }

    return -1;
  };

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

  const parseAssistantEditNote = (content: string) => {
    const englishMatch = content.match(/^Assistant\s+message\s+edited:\s+([^\.\n]+)\./i);
    const vietnameseMatch = content.match(/^Chat\s+co\s+messageID\s+duoc\s+sua\s+thanh\s+([^\.\n]+)\./i);
    const idMatch = englishMatch ?? vietnameseMatch;

    if (!idMatch) {
      return null;
    }

    const messageId = idMatch[1].trim();
    if (!messageId) {
      return null;
    }

    const englishCombinedMatch = content.match(/New\s+content:\s*([\s\S]*?)(?:\nNew\s+pinyin:\s*([\s\S]*))?$/i);
    const vietnameseContentMatch = content.match(/Noi\s+dung\s+moi:\s*([\s\S]+)/i);
    const updatedText = englishCombinedMatch
      ? englishCombinedMatch[1].trim()
      : (vietnameseContentMatch ? vietnameseContentMatch[1].trim() : "");
    if (!updatedText) {
      return null;
    }

    const updatedPinyin = englishCombinedMatch ? (englishCombinedMatch[2] ?? "").trim() : undefined;

    return { messageId, updatedText, updatedPinyin };
  };

  const applyAssistantEdits = (history: { role: string; content: string }[]) => {
    const edits = new Map<string, { text: string; pinyin?: string }>();

    for (const message of history) {
      if (message.role !== "developer") {
        continue;
      }

      const edit = parseAssistantEditNote(message.content);
      if (edit) {
        edits.set(edit.messageId, {
          text: edit.updatedText,
          pinyin: edit.updatedPinyin
        });
      }
    }

    if (!edits.size) {
      return history;
    }

    return history.map((message) => {
      if (message.role !== "assistant") {
        return message;
      }

      const turns = parseAssistantReply(message.content);
      if (!turns.length) {
        return message;
      }

      let didUpdate = false;
      const nextTurns = turns.map((turn) => {
        const turnId = typeof turn.MessageId === "string" ? turn.MessageId.trim() : "";
        const updatedEdit = turnId ? edits.get(turnId) : null;
        const updatedText = updatedEdit?.text ?? "";

        if (updatedText) {
          didUpdate = true;
          const nextTurn: AssistantTurn = { ...turn, Text: updatedText };

          if (updatedEdit && updatedEdit.pinyin !== undefined) {
            nextTurn.Pinyin = updatedEdit.pinyin;
          }

          return nextTurn;
        }

        return turn;
      });

      if (!didUpdate) {
        return message;
      }

      return {
        ...message,
        content: JSON.stringify(nextTurns)
      };
    });
  };

  /**
   * Builds a dynamic system instruction string for the current user/session.
   *
   * This mirrors the older MimiChat initChat prompt style (level rules, characters, optional
   * story/context blocks), while skipping any missing fields.
   */
  const buildSystemPrompt = async (userId: number, body: unknown, userMessage?: string): Promise<{ prompt: string; activeCharacters: string[] }> => {
    if (deps.systemPromptBuilder) {
      const prompt = await deps.systemPromptBuilder({ userId, body });
      return { prompt: (prompt ?? "").trim(), activeCharacters: [] };
    }

    const payload = (body ?? {}) as Record<string, unknown>;

    const user = await userRepository.findOne({
      where: { id: userId },
      relations: { level: true }
    });

    const story = await loadStoryForPrompt(userId);
    console.log("Loaded story for prompt:", story);

    // Retrieve long-term memory brief if memory services are available
    let longTermMemoryBrief: string | null = null;

    // Extract active character names from developer messages in history
    // (needed for both memory retrieval and relationship block)
    const history = await historyStore.load(userId);
    const activeCharMap = new Map<string, boolean>();
    for (const msg of history) {
      if (msg.role !== "developer") continue;
      const action = parseDeveloperCharacterAction(msg.content);
      if (action?.name) activeCharMap.set(action.name, action.active);
    }
    const activeCharacters = Array.from(activeCharMap.entries())
      .filter(([, active]) => active)
      .map(([name]) => name);

    if (memoryRetrievalService && userMessage) {
      try {
        longTermMemoryBrief = await memoryRetrievalService.retrieveMemoryBrief(
          userId,
          userMessage,
          history,
          { storyId: story?.id ?? null, activeCharacters }
        ) || null;
      } catch (error) {
        console.warn("Memory retrieval failed, proceeding without long-term memory:", error);
      }
    }

    // Build structured relationship block from DB for active characters
    let relationshipSummary: string | null = null;
    try {
      relationshipSummary = await buildRelationshipBlock(userId, activeCharacters);
    } catch (error) {
      console.warn("Relationship block build failed, proceeding without:", error);
    }

    return {
      prompt: buildChatSystemPrompt({
        level: user?.level?.level ?? null,
        levelMaxWords: user?.level?.maxWords ?? null,
        levelDescription: user?.level?.descript ?? null,
        levelGuideline: user?.level?.guideline ?? null,
        userName: user?.name ?? null,
        userAge: user?.age ?? null,
        userDescription: user?.description ?? null,
        context: getOptionalString(payload.context) || null,
        storyPlot: story?.name || null,
        storyDescription: story?.description || null,
        storyProgress: story?.currentProgress ?? null,
        relationshipSummary,
        contextSummary: getOptionalString(payload.contextSummary) || null,
        relatedStoryMessages: getOptionalString(payload.relatedStoryMessages) || null,
        checkPronunciation: Boolean(payload.checkPronunciation),
        longTermMemoryBrief,
        activeCharacterNames: activeCharacters,
      }),
      activeCharacters,
    };
  };

  const sendMessage: ChatController["sendMessage"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const message = typeof request.body?.message === "string" ? request.body.message.trim() : "";
    const sessionId = getSessionId(request.body?.sessionId);
    const modelOverride = "gemini-3.1-flash-lite-preview";
    const audioBase64 = typeof request.body?.audio === "string" ? request.body.audio.trim() : "";
    const hasAudio = Boolean(audioBase64);

    if (!message && !hasAudio) {
      response.status(400).json({
        message: "Message or audio is required"
      });
      return;
    }

    // Determine which AI service to use based on model
    const useGemini = isGeminiModel(modelOverride || openAIModel);
    const selectedService = useGemini ? geminiService : openAIService;
    const serviceName = useGemini ? "Gemini" : "OpenAI";

    if (!selectedService) {
      response.status(500).json({
        message: `${serviceName} API key is not configured`
      });
      return;
    }

    try {
      // Parse audio data URL into inline data for Gemini if present
      let geminiAudioParts: GeminiAudioPart[] | undefined;
      if (hasAudio && useGemini) {
        const parsed = parseAudioDataUrl(audioBase64);
        if (!parsed) {
          response.status(400).json({ message: "Invalid audio data URL" });
          return;
        }
        if (parsed.buffer.byteLength > MAX_AUDIO_BYTES) {
          response.status(413).json({ message: "Audio payload is too large" });
          return;
        }
        geminiAudioParts = [{
          data: parsed.buffer.toString("base64"),
          mimeType: parsed.mime
        }];
      }

      // When audio is present, instruct Gemini to also transcribe the audio
      const audioTranscribeInstruction = hasAudio
        ? "\n\nIMPORTANT: The user has sent an audio recording. Listen to the audio carefully and include an extra field \"Transcribe\" in EVERY item of your JSON response array. \"Transcribe\" must contain the exact transcription of what the user said in the audio. If the audio is unclear, transcribe as best you can. Respond to the audio content naturally as if the user typed it."
        : "";

      const effectiveMessage = (message || "The user sent an audio message. Please listen and respond.") + audioTranscribeInstruction;

      console.log("[Chat] User message:", message || "<audio>");

      const { prompt: systemPrompt, activeCharacters } = await buildSystemPrompt(request.user.id, request.body, message);
      await historyStore.ensureSystemMessage(request.user.id, systemPrompt);
      const history = await historyStore.load(request.user.id);
      console.log("[Chat] Sending to AI — model:", modelOverride || openAIModel, "| message:", effectiveMessage.slice(0, 300));

      const result = await requestJsonReplyWithRetry(
        selectedService,
        effectiveMessage,
        history,
        modelOverride || undefined,
        2,
        geminiAudioParts,
        `chat_${request.user.id}`,
        activeCharacters
      );

      console.log("[Chat] AI response (model:", result.model, "):", result.reply.slice(0, 500));

      const normalizedReply = useGemini
        ? normalizeAssistantReplyMessageIds(result.reply, collectAssistantMessageIds(history))
        : result.reply;

      // Extract transcription from the first turn if present
      let transcribe: string | undefined;
      if (hasAudio) {
        const turns = parseAssistantReply(normalizedReply);
        for (const turn of turns) {
          const t = typeof (turn as Record<string, unknown>).Transcribe === "string"
            ? ((turn as Record<string, unknown>).Transcribe as string).trim()
            : "";
          if (t) {
            transcribe = t;
            break;
          }
        }
      }

      // Extract and store memory sidecars (fire-and-forget, non-blocking)
      let cleanReply = normalizedReply;
      if (vectorMemoryService) {
        const turns = parseAssistantReply(normalizedReply);
        const candidates = extractMemorySidecars(turns);
        const itemsToStore = candidates.filter(shouldStoreMemory);
        if (itemsToStore.length) {
          const user = await userRepository.findOne({ where: { id: request.user!.id } });
          const storyId = user?.currentStoryId ?? null;
          const items = itemsToStore.map((c) => buildMemoryItemFromCandidate(c, request.user!.id, storyId, message));
          vectorMemoryService
            .upsert(request.user!.id, items)
            .then(() => {
              console.log("[Memory] Chroma upsert success:", {
                userId: request.user!.id,
                storyId,
                itemCount: items.length,
                source: "sendMessage"
              });
            })
            .catch((err) => {
              console.warn("[Memory] Chroma upsert failed:", {
                userId: request.user!.id,
                storyId,
                itemCount: items.length,
                source: "sendMessage",
                error: err instanceof Error ? err.message : String(err)
              });
            });
        }
        cleanReply = stripMemorySidecar(normalizedReply);
      }

      // Store the transcribed text as the user message in history (not the raw instruction)
      const userHistoryContent = hasAudio && transcribe ? transcribe : message;
      await historyStore.append(request.user.id, [
        { role: "user", content: userHistoryContent },
        { role: "assistant", content: cleanReply }
      ]);

      response.json({
        reply: cleanReply,
        model: result.model,
        ...(transcribe ? { transcribe } : {})
      });
    } catch (error) {
      console.error("Error in sendMessage:", error);
      response.status(500).json({
        message: "Failed to generate reply",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  const respondFromHistory: ChatController["respondFromHistory"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const modelOverride = "gemini-3.1-flash-lite-preview";

    const useGemini = isGeminiModel(modelOverride || openAIModel);
    const selectedService = useGemini ? geminiService : openAIService;
    const serviceName = useGemini ? "Gemini" : "OpenAI";

    if (!selectedService) {
      response.status(500).json({
        message: `${serviceName} API key is not configured`
      });
      return;
    }

    try {
      const fallbackMessage = "Continue the conversation naturally based on the current context.";
      const { prompt: systemPrompt, activeCharacters } = await buildSystemPrompt(request.user.id, request.body, fallbackMessage);
      await historyStore.ensureSystemMessage(request.user.id, systemPrompt);
      const history = await historyStore.load(request.user.id);
      const result = await requestJsonReplyWithRetry(
        selectedService,
        fallbackMessage,
        history,
        modelOverride || undefined,
        2,
        undefined,
        `chat_${request.user.id}`,
        activeCharacters
      );

      const normalizedReply = useGemini
        ? normalizeAssistantReplyMessageIds(result.reply, collectAssistantMessageIds(history))
        : result.reply;

      // Extract and store memory sidecars (fire-and-forget, non-blocking)
      let cleanReply = normalizedReply;
      if (vectorMemoryService) {
        const turns = parseAssistantReply(normalizedReply);
        const candidates = extractMemorySidecars(turns);
        const itemsToStore = candidates.filter(shouldStoreMemory);
        if (itemsToStore.length) {
          const user = await userRepository.findOne({ where: { id: request.user!.id } });
          const storyId = user?.currentStoryId ?? null;
          const items = itemsToStore.map((c) => buildMemoryItemFromCandidate(c, request.user!.id, storyId));
          vectorMemoryService
            .upsert(request.user!.id, items)
            .then(() => {
              console.log("[Memory] Chroma upsert success:", {
                userId: request.user!.id,
                storyId,
                itemCount: items.length,
                source: "respondFromHistory"
              });
            })
            .catch((err) => {
              console.warn("[Memory] Chroma upsert failed:", {
                userId: request.user!.id,
                storyId,
                itemCount: items.length,
                source: "respondFromHistory",
                error: err instanceof Error ? err.message : String(err)
              });
            });
        }
        cleanReply = stripMemorySidecar(normalizedReply);
      }

      await historyStore.append(request.user.id, [{ role: "assistant", content: cleanReply }]);

      response.json({
        reply: cleanReply,
        model: result.model
      });
    } catch (error) {
      console.error("Error in respondFromHistory:", error);
      response.status(500).json({
        message: "Failed to generate reply",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  const getHistory: ChatController["getHistory"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const sessionId = getSessionId(request.query?.sessionId);

    try {
      const messages = await historyStore.load(request.user.id);
      const adjustedMessages = applyAssistantEdits(messages);
      response.json({
        messages: adjustedMessages.filter((message) => message.role !== "system" && message.role !== "developer")
      });
    } catch (error) {
      console.error("Error in getHistory:", error);
      response.status(500).json({
        message: "Failed to load chat history",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  const appendDeveloperMessage: ChatController["appendDeveloperMessage"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const payload = (request.body ?? {}) as Record<string, unknown>;
    const sessionId = getSessionId(payload.sessionId);
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
      const message = kind === "context_update"
        ? "Context is required"
        : "Character name is required";
      response.status(400).json({ message });
      return;
    }

    try {
      await historyStore.append(request.user.id, [{ role: "developer", content }]);
      response.json({ ok: true });
    } catch (error) {
      console.error("Error in appendDeveloperMessage:", error);
      response.status(500).json({
        message: "Failed to append developer message",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Applies edits to a user or assistant message and refreshes history as needed.
   */
  const editMessage: ChatController["editMessage"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const payload = (request.body ?? {}) as Record<string, unknown>;
    const sessionId = getSessionId(payload.sessionId);
    const kind = typeof payload.kind === "string" ? payload.kind.trim() : "";
    const modelOverride = getOptionalString(payload.model);

    if (kind !== "user" && kind !== "assistant") {
      response.status(400).json({ message: "Invalid edit kind" });
      return;
    }

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
        console.error("Error in editMessage (assistant):", error);
        response.status(500).json({
          message: "Failed to append developer message",
          error: error instanceof Error ? error.message : "Unknown error"
        });
      }

      return;
    }

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

    // Determine which AI service to use based on model
    const useGemini = isGeminiModel(modelOverride || openAIModel);
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
      const { prompt: systemPrompt, activeCharacters } = await buildSystemPrompt(request.user.id, request.body);
      const prefixWithoutSystem = prefix.filter((message) => message.role !== "system");
      const historyForAI: ChatHistoryMessage[] = [
        { role: "system", content: systemPrompt },
        ...prefixWithoutSystem
      ];

      await historyStore.clear(request.user.id);
      // Invalidate cached Gemini chat session since history was rewritten
      if (geminiService) {
        geminiService.clearSession(`chat_${request.user.id}`);
      }
      await historyStore.ensureSystemMessage(request.user.id, systemPrompt);

      const result = await requestJsonReplyWithRetry(
        selectedService,
        editedContent,
        historyForAI,
        modelOverride || undefined,
        2,
        undefined,
        `chat_${request.user.id}`,
        activeCharacters
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

      response.json({
        messages: nextMessages.filter((message) => message.role !== "system" && message.role !== "developer"),
        reply: normalizedReply,
        model: result.model
      });
    } catch (error) {
      console.error("Error in editMessage (user):", error);
      response.status(500).json({
        message: "Failed to edit chat message",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  const getDeveloperState: ChatController["getDeveloperState"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const sessionId = getSessionId(request.query?.sessionId);

    try {
      const messages = await historyStore.load(request.user.id);
      const activeMap = new Map<string, boolean>();

      for (const message of messages) {
        if (message.role !== "developer") {
          continue;
        }

        const action = parseDeveloperCharacterAction(message.content);
        if (action?.name) {
          activeMap.set(action.name, action.active);
        }
      }

      const activeCharacterNames = Array.from(activeMap.entries())
        .filter(([, isActive]) => isActive)
        .map(([name]) => name);

      response.json({
        activeCharacterNames
      });
    } catch (error) {
      console.error("Error in getDeveloperState:", error);
      response.status(500).json({
        message: "Failed to load developer state",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Transcribes recorded user audio into text for chat input.
   */
  const transcribeAudio: ChatController["transcribeAudio"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const payload = (request.body ?? {}) as { audio?: unknown };
    const audio = typeof payload.audio === "string" ? payload.audio : "";

    if (!audio.trim()) {
      response.status(400).json({ message: "audio is required" });
      return;
    }

    const parsed = parseAudioDataUrl(audio);
    if (!parsed) {
      response.status(400).json({ message: "Invalid audio data URL" });
      return;
    }

    if (parsed.buffer.byteLength > MAX_AUDIO_BYTES) {
      response.status(413).json({ message: "Audio payload is too large" });
      return;
    }

    try {
      const extension = resolveAudioExtension(parsed.mime);
      const file = await toFile(parsed.buffer, `chat.${extension}`, { type: parsed.mime });
      const transcript = await transcribeWithOpenAI(file, DEFAULT_TRANSCRIBE_LANGUAGE);
      response.json({ transcript });
    } catch (error) {
      console.error("Error in transcribeAudio:", error);
      response.status(500).json({ message: "Failed to transcribe audio" });
    }
  };

  /**
   * Returns the system prompt and chat history so a local AI client (e.g. Ollama on PC)
   * can generate a reply without the server calling a cloud AI service.
   */
  const prepareLocalPrompt: ChatController["prepareLocalPrompt"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const message = typeof request.body?.message === "string" ? request.body.message.trim() : "";

    try {
      const { prompt: systemPrompt, activeCharacters } = await buildSystemPrompt(request.user.id, request.body, message || undefined);
      await historyStore.ensureSystemMessage(request.user.id, systemPrompt);
      const history = await historyStore.load(request.user.id);

      response.json({
        systemPrompt,
        history: history.filter(m => m.role !== "developer"),
        activeCharacters
      });
    } catch (error) {
      console.error("Error in prepareLocalPrompt:", error);
      response.status(500).json({
        message: "Failed to prepare local prompt",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Saves a user message and a locally-generated assistant reply to chat history.
   * Also performs memory extraction if ChromaDB is configured.
   */
  const saveLocalReply: ChatController["saveLocalReply"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const message = typeof request.body?.message === "string" ? request.body.message.trim() : "";
    const reply = typeof request.body?.reply === "string" ? request.body.reply.trim() : "";

    if (!reply) {
      response.status(400).json({ message: "Reply is required" });
      return;
    }

    try {
      // Extract and store memory sidecars (fire-and-forget, non-blocking)
      let cleanReply = reply;
      if (vectorMemoryService) {
        const turns = parseAssistantReply(reply);
        const candidates = extractMemorySidecars(turns);
        const itemsToStore = candidates.filter(shouldStoreMemory);
        if (itemsToStore.length) {
          const user = await userRepository.findOne({ where: { id: request.user!.id } });
          const storyId = user?.currentStoryId ?? null;
          const items = itemsToStore.map((c) => buildMemoryItemFromCandidate(c, request.user!.id, storyId, message));
          vectorMemoryService
            .upsert(request.user!.id, items)
            .then(() => {
              console.log("[Memory] Chroma upsert success:", {
                userId: request.user!.id,
                storyId,
                itemCount: items.length,
                source: "saveLocalReply"
              });
            })
            .catch((err) => {
              console.warn("[Memory] Chroma upsert failed:", {
                userId: request.user!.id,
                storyId,
                itemCount: items.length,
                source: "saveLocalReply",
                error: err instanceof Error ? err.message : String(err)
              });
            });
        }
        cleanReply = stripMemorySidecar(reply);
      }

      const messagesToAppend: ChatHistoryMessage[] = [];
      if (message) {
        messagesToAppend.push({ role: "user", content: message });
      }
      messagesToAppend.push({ role: "assistant", content: cleanReply });

      await historyStore.append(request.user.id, messagesToAppend);

      response.json({ ok: true, reply: cleanReply });
    } catch (error) {
      console.error("Error in saveLocalReply:", error);
      response.status(500).json({
        message: "Failed to save local reply",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return {
    sendMessage,
    respondFromHistory,
    getHistory,
    appendDeveloperMessage,
    editMessage,
    getDeveloperState,
    transcribeAudio,
    prepareLocalPrompt,
    saveLocalReply
  };
};