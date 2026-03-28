import type { Request, Response } from "express";
import { randomUUID } from "crypto";
import { IsNull, type DataSource } from "typeorm";
import { toFile, type Uploadable } from "openai/uploads";
import { createOpenAIChatService, createOpenAIClient, type OpenAIChatService } from "../../services/openai.service.js";
import { createGeminiChatService, isGeminiModel, type GeminiChatService, type GeminiAudioPart } from "../../services/gemini.service.js";
import { buildChatSystemPrompt } from "../../services/chat-prompt.service.js";
import StoryEntity from "../../models/story.entity.js";
import UserEntity from "../../models/user.entity.js";
import { createChatHistoryStore, type ChatHistoryMessage, type ChatHistoryStore } from "../../services/chat-history.service.js";
import {
  parseAssistantReply,
  parseAssistantEditNote,
  applyAssistantEdits,
  parseDeveloperCharacterAction,
  parseDeveloperLearningPathState,
  buildMessageEntities,
  normalizeName
} from "../../utils/chat.utils.js";
import MessageEntity from "../../models/message.entity.js";
import CharacterEntity from "../../models/character.entity.js";

interface ChatController {
  sendMessage: (request: Request, response: Response) => Promise<void>;
  respondFromHistory: (request: Request, response: Response) => Promise<void>;
  getHistory: (request: Request, response: Response) => Promise<void>;
  appendDeveloperMessage: (request: Request, response: Response) => Promise<void>;
  editMessage: (request: Request, response: Response) => Promise<void>;
  getDeveloperState: (request: Request, response: Response) => Promise<void>;
  transcribeAudio: (request: Request, response: Response) => Promise<void>;
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
}

interface AssistantTurn {
  MessageId?: string;
  CharacterName?: string;
  Text?: string;
  Pinyin?: string;
  Tone?: string;
  Translation?: string;
  Transcribe?: string;
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
  const messageRepository = dataSource.getRepository(MessageEntity);
  const characterRepository = dataSource.getRepository(CharacterEntity);
  
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

  const handleAutoSummaryAndSave = async (userId: number, systemPrompt: string, selectedService: ChatReplyService, modelOverride: string) => {
    try {
      const updatedHistory = await historyStore.load(userId);
      const userMessageCount = updatedHistory.filter((msg) => msg.role === "user").length;

      if (userMessageCount >= 3) {
        const adjustedHistory = applyAssistantEdits(updatedHistory);
        const summaryInstruction = `
Please summarize the above conversation in Vietnamese, update the story description, and return it in JSON format as follows:
{
  "Summary": "Summary of the conversation here.", 
  "UpdatedStoryDescription": "The story description has been updated here." 
}
`.trim();

        const summaryReply = await selectedService.createReply(undefined, [
          ...adjustedHistory,
          { role: "developer", content: summaryInstruction }
        ], modelOverride || undefined, undefined, `summary_${userId}`);
        
        let cleanReply = summaryReply.reply;
        cleanReply = cleanReply.replace(/```json/i, "").replace(/```/g, "").trim();
        const summaryStory = JSON.parse(cleanReply) as { Summary: string; UpdatedStoryDescription: string };

        // Update story progress
        const story = await loadStoryForPrompt(userId);
        if (story) {
          const updatedProgress = summaryStory.UpdatedStoryDescription;
          if (updatedProgress) {
            story.currentProgress = updatedProgress;
            await storyRepository.save(story);
          }
        }

        // Save messages to DB with journalId = null
        const characters = await characterRepository.find({ where: { userId } });
        const voiceByCharacter = new Map(
          characters
            .filter((character) => character.voiceName)
            .map((character) => [normalizeName(character.name), {
              voiceModel: character.voiceModel ?? "openai",
              voiceName: character.voiceName as string,
              pitch: character.pitch ?? null,
              speakingRate: character.speakingRate ?? null,
            }])
        );
        
        const messageEntities = buildMessageEntities(adjustedHistory, userId, null, voiceByCharacter);
        if (messageEntities.length) {
          await messageRepository.save(messageEntities.map((msg) => messageRepository.create(msg)));
        }

        // Determine active developer state (characters, learning path)
        const activeMap = new Map<string, boolean>();
        let activeLearningPathId: number | null = null;
        let activeLearningPathContext = "";
        let activeLearningPathVocabulary = "";

        for (const msg of adjustedHistory) {
          if (msg.role !== "developer") continue;

          const action = parseDeveloperCharacterAction(msg.content);
          if (action?.name) {
            activeMap.set(action.name, action.active);
          }

          const learningPathState = parseDeveloperLearningPathState(msg.content);
          if (learningPathState) {
            activeLearningPathId = learningPathState.learningPathId;
            activeLearningPathContext = learningPathState.context;
            activeLearningPathVocabulary = learningPathState.vocabulary;
          }
        }

        const activeCharacterNames = Array.from(activeMap.entries())
          .filter(([, isActive]) => isActive)
          .map(([name]) => name);

        // Clear chat history
        await historyStore.clear(userId);
        if (geminiService) {
          geminiService.clearSession(`chat_${userId}`);
        }

        // Re-insert system message and current active state
        await historyStore.ensureSystemMessage(userId, systemPrompt);
        
        const newMessages: ChatHistoryMessage[] = [];
        
        if (summaryStory.Summary) {
          newMessages.push({ role: "developer", content: `Developer context update:\n${summaryStory.Summary}` });
        }

        for (const name of activeCharacterNames) {
          const char = characters.find(c => normalizeName(c.name) === normalizeName(name));
          if (char) {
             newMessages.push({ role: "developer", content: formatCharacterAddedMessage({ character: char }) });
          } else {
             newMessages.push({ role: "developer", content: `Character "${name}" has been added.` });
          }
        }

        if (activeLearningPathId) {
           newMessages.push({
             role: "developer",
             content: formatLearningPathAppliedMessage({
               learningPathId: activeLearningPathId,
               context: activeLearningPathContext,
               vocabulary: activeLearningPathVocabulary
             })
           });
        }

        if (newMessages.length > 0) {
          await historyStore.append(userId, newMessages);
        }
      }
    } catch (error) {
      console.error("Failed to auto-summarize and clear history:", error);
    }
  };

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
   * Builds a developer message when a learning path is applied to chat.
   *
   * @param payload - Request payload containing learning path id/context/vocabulary.
   * @returns A formatted developer message or empty string when invalid.
   */
  const formatLearningPathAppliedMessage = (payload: Record<string, unknown>) => {
    const learningPathIdRaw = typeof payload.learningPathId === "number"
      ? payload.learningPathId
      : Number.parseInt(String(payload.learningPathId ?? ""), 10);
    const learningPathId = Number.isInteger(learningPathIdRaw) && learningPathIdRaw > 0 ? learningPathIdRaw : null;
    const context = typeof payload.context === "string" ? payload.context.trim() : "";
    const vocabulary = typeof payload.vocabulary === "string" ? payload.vocabulary.trim() : "";

    if (!learningPathId || !context || !vocabulary) {
      return "";
    }

    return [
      "Developer learning path applied:",
      `LearningPathId: ${learningPathId}`,
      "Please note that the message must follow the correct sequence from beginning to end; no stages should be skipped.",
      "LearningPathContext:",
      context,
      "LearningPathVocabulary:",
      vocabulary,
      "AI requirements:",
      "- Follow the learning-path context strictly.",
      "- Use the provided vocabulary naturally in the dialogue.",
      "- Do not drift outside the selected learning path context."
    ].join("\n");
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
   * Requests a reply from AI and retries with a corrective prompt when JSON format is invalid.
   *
   * @param service - Target chat service.
   * @param message - User message to send.
   * @param history - Message history.
   * @param modelOverride - Optional model override.
   * @param retryLimit - Max retry attempts after the initial call.
   * @returns JSON-valid assistant reply result.
   */
  const requestJsonReplyWithRetry = async (
    service: ChatReplyService,
    message: string | undefined,
    history: Array<{ role: "system" | "developer" | "user" | "assistant"; content: string }>,
    modelOverride?: string,
    retryLimit = 2,
    audioParts?: GeminiAudioPart[],
    sessionKey?: string
  ): Promise<JsonReplyResult> => {
    let attempt = 0;
    let prompt = typeof message === "string" && message.trim() ? message.trim() : undefined;
    const retryPromptSeed = prompt || "Continue the conversation naturally based on the current chat history.";
    let lastResult: JsonReplyResult | null = null;

    while (attempt <= retryLimit) {
      // Only send audio on the first attempt; retries use text-only corrective prompts
      const audioForAttempt = attempt === 0 ? audioParts : undefined;
      const result = await service.createReply(prompt, history, modelOverride || undefined, audioForAttempt, sessionKey);
      lastResult = result;

      const turns = parseAssistantReply(result.reply);
      if (isValidAssistantTurnSchema(turns)) {
        return result;
      }

      attempt += 1;
      if (attempt > retryLimit) {
        break;
      }

      console.warn(`Assistant reply JSON invalid at attempt ${attempt}. Retrying with strict JSON format reminder.`);
      prompt = buildJsonRetryPrompt(retryPromptSeed);
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

  /**
   * Builds a dynamic system instruction string for the current user/session.
   *
   * This mirrors the older MimiChat initChat prompt style (level rules, characters, optional
   * story/context blocks), while skipping any missing fields.
   */
  const buildSystemPrompt = async (userId: number, body: unknown) => {
    if (deps.systemPromptBuilder) {
      const prompt = await deps.systemPromptBuilder({ userId, body });
      return (prompt ?? "").trim();
    }

    const payload = (body ?? {}) as Record<string, unknown>;

    const user = await userRepository.findOne({
      where: { id: userId },
      relations: { level: true }
    });

    const story = await loadStoryForPrompt(userId);
    console.log("Loaded story for prompt:", story);
    return buildChatSystemPrompt({
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
      relationshipSummary: getOptionalString(payload.relationshipSummary) || null,
      contextSummary: getOptionalString(payload.contextSummary) || null,
      relatedStoryMessages: getOptionalString(payload.relatedStoryMessages) || null,
      checkPronunciation: Boolean(payload.checkPronunciation)
    });
  };

  const sendMessage: ChatController["sendMessage"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const message = typeof request.body?.message === "string" ? request.body.message.trim() : "";
    const sessionId = getSessionId(request.body?.sessionId);
    const modelOverride = "gemini-3-flash-preview";
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

      const systemPrompt = await buildSystemPrompt(request.user.id, request.body);
      await historyStore.ensureSystemMessage(request.user.id, systemPrompt);
      const history = await historyStore.load(request.user.id);
      const result = await requestJsonReplyWithRetry(
        selectedService,
        effectiveMessage,
        history,
        modelOverride || undefined,
        2,
        geminiAudioParts,
        `chat_${request.user.id}`
      );

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

      // Store the transcribed text as the user message in history (not the raw instruction)
      const userHistoryContent = hasAudio && transcribe ? transcribe : message;
      await historyStore.append(request.user.id, [
        { role: "user", content: userHistoryContent },
        { role: "assistant", content: normalizedReply }
      ]);

      await handleAutoSummaryAndSave(request.user.id, systemPrompt, selectedService, modelOverride || "");

      response.json({
        reply: normalizedReply,
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

    const modelOverride = "gemini-3-flash-preview";

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
      const systemPrompt = await buildSystemPrompt(request.user.id, request.body);
      await historyStore.ensureSystemMessage(request.user.id, systemPrompt);
      const history = await historyStore.load(request.user.id);
      const result = await requestJsonReplyWithRetry(
        selectedService,
        fallbackMessage,
        history,
        modelOverride || undefined,
        2,
        undefined,
        `chat_${request.user.id}`
      );

      const normalizedReply = useGemini
        ? normalizeAssistantReplyMessageIds(result.reply, collectAssistantMessageIds(history))
        : result.reply;

      await historyStore.append(request.user.id, [{ role: "assistant", content: normalizedReply }]);

      response.json({
        reply: normalizedReply,
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
      const dbMessages = await messageRepository.find({
        where: { userId: request.user.id, journalId: IsNull() },
        order: { createdAt: "ASC" }
      });

      const dbHistory: ChatHistoryMessage[] = dbMessages.map(msg => {
        if (msg.characterName === "User") {
          return { role: "user", content: msg.content };
        } else {
          const turn = {
            MessageId: msg.id,
            CharacterName: msg.characterName,
            Text: msg.content,
            Pinyin: msg.pinyin || "",
            Tone: msg.tone || "",
            Translation: msg.translation || "",
            ...(msg.audio ? { Audio: msg.audio } : {})
          };
          return { role: "assistant", content: JSON.stringify([turn]) };
        }
      });

      const activeMessages = await historyStore.load(request.user.id);
      
      const combinedMessages = [...dbHistory, ...activeMessages];
      const adjustedCombinedMessages = applyAssistantEdits(combinedMessages);
      
      const finalHistory = adjustedCombinedMessages.filter(
        (message) => message.role !== "system" && message.role !== "developer"
      );

      response.json({
        messages: finalHistory
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

    if (kind !== "character_added" && kind !== "character_removed" && kind !== "context_update" && kind !== "learning_path_apply") {
      response.status(400).json({ message: "Invalid developer message kind" });
      return;
    }

    const content =
      kind === "character_added"
        ? formatCharacterAddedMessage(payload)
        : kind === "character_removed"
        ? formatCharacterRemovedMessage(payload)
        : kind === "learning_path_apply"
        ? formatLearningPathAppliedMessage(payload)
        : formatContextMessage(payload);

    if (!content) {
      const message = kind === "context_update"
        ? "Context is required"
        : kind === "learning_path_apply"
        ? "LearningPathId, context, and vocabulary are required"
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
      const dbUserCount = await messageRepository.count({
        where: { userId: request.user.id, journalId: IsNull(), characterName: "User" }
      });
      
      const history = await historyStore.load(request.user.id);
      const adjustedUserIndex = userIndex - dbUserCount;
      
      if (adjustedUserIndex < 0) {
        response.status(400).json({ message: "Cannot edit summarized history" });
        return;
      }
      
      const targetIndex = findUserHistoryIndex(history, adjustedUserIndex);

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
        `chat_${request.user.id}`
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

      await handleAutoSummaryAndSave(request.user.id, systemPrompt, selectedService, modelOverride || "");

      const dbMessages = await messageRepository.find({
        where: { userId: request.user.id, journalId: IsNull() },
        order: { createdAt: "ASC" }
      });

      const dbHistory: ChatHistoryMessage[] = dbMessages.map(msg => {
        if (msg.characterName === "User") {
          return { role: "user", content: msg.content };
        } else {
          const turn = {
            MessageId: msg.id,
            CharacterName: msg.characterName,
            Text: msg.content,
            Pinyin: msg.pinyin || "",
            Tone: msg.tone || "",
            Translation: msg.translation || "",
            ...(msg.audio ? { Audio: msg.audio } : {})
          };
          return { role: "assistant", content: JSON.stringify([turn]) };
        }
      });
      
      const updatedActiveMessages = await historyStore.load(request.user.id);
      const finalCombinedMessages = applyAssistantEdits([...dbHistory, ...updatedActiveMessages]);
      const finalActiveHistory = finalCombinedMessages.filter(
        (message) => message.role !== "system" && message.role !== "developer"
      );

      response.json({
        messages: finalActiveHistory,
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

      let activeLearningPathId: number | null = null;
      let activeLearningPathContext = "";
      let activeLearningPathVocabulary = "";

      for (const message of messages) {
        if (message.role !== "developer") {
          continue;
        }

        const learningPathState = parseDeveloperLearningPathState(message.content);
        if (!learningPathState) {
          continue;
        }

        activeLearningPathId = learningPathState.learningPathId;
        activeLearningPathContext = learningPathState.context;
        activeLearningPathVocabulary = learningPathState.vocabulary;
      }

      response.json({
        activeCharacterNames,
        activeLearningPathId,
        activeLearningPathContext,
        activeLearningPathVocabulary
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

  return {
    sendMessage,
    respondFromHistory,
    getHistory,
    appendDeveloperMessage,
    editMessage,
    getDeveloperState,
    transcribeAudio
  };
};