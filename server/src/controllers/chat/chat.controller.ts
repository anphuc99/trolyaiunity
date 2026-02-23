import type { Request, Response } from "express";
import type { DataSource } from "typeorm";
import { createOpenAIChatService, type OpenAIChatService } from "../../services/openai.service.js";
import { createGeminiChatService, isGeminiModel, type GeminiChatService } from "../../services/gemini.service.js";
import { buildChatSystemPrompt } from "../../services/chat-prompt.service.js";
import StoryEntity from "../../models/story.entity.js";
import UserEntity from "../../models/user.entity.js";
import { createChatHistoryStore, type ChatHistoryMessage, type ChatHistoryStore } from "../../services/chat-history.service.js";

interface ChatController {
  sendMessage: (request: Request, response: Response) => Promise<void>;
  getHistory: (request: Request, response: Response) => Promise<void>;
  appendDeveloperMessage: (request: Request, response: Response) => Promise<void>;
  editMessage: (request: Request, response: Response) => Promise<void>;
  getDeveloperState: (request: Request, response: Response) => Promise<void>;
}

interface ChatControllerDeps {
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
  
  const historyStore = deps.historyStore ?? createChatHistoryStore();

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
   * Parses a numeric story id from user input.
   *
   * @param value - Input value from the request body.
   * @returns Story id or null when invalid.
   */
  const parseStoryId = (value: unknown) => {
    const parsed = typeof value === "number" ? value : Number.parseInt(String(value ?? ""), 10);
    if (!Number.isInteger(parsed) || parsed <= 0) {
      return null;
    }
    return parsed;
  };

  /**
   * Loads the story used for prompt enrichment.
   *
   * @param userId - Authenticated user id.
   * @param payload - Request payload for the chat call.
   * @returns The matching story or null when not found.
   */
  const loadStoryForPrompt = async (userId: number, payload: Record<string, unknown>) => {
    const storyId = parseStoryId(payload.storyId);
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
    const appearance = typeof character.appearance === "string" ? character.appearance.trim() : "";

    if (!name) {
      return "";
    }

    const lines = [`Character \"${name}\" has been added.`];

    if (gender) {
      lines.push(`Gender: ${gender}`);
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

    const englishContentMatch = content.match(/New\s+content:\s*([\s\S]+)/i);
    const vietnameseContentMatch = content.match(/Noi\s+dung\s+moi:\s*([\s\S]+)/i);
    const contentMatch = englishContentMatch ?? vietnameseContentMatch;
    const updatedText = contentMatch ? contentMatch[1].trim() : "";
    if (!updatedText) {
      return null;
    }

    return { messageId, updatedText };
  };

  const applyAssistantEdits = (history: { role: string; content: string }[]) => {
    const edits = new Map<string, string>();

    for (const message of history) {
      if (message.role !== "developer") {
        continue;
      }

      const edit = parseAssistantEditNote(message.content);
      if (edit) {
        edits.set(edit.messageId, edit.updatedText);
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
        const updatedText = turnId ? edits.get(turnId) : null;

        if (updatedText) {
          didUpdate = true;
          return { ...turn, Text: updatedText };
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

    const story = await loadStoryForPrompt(userId, payload);

    return buildChatSystemPrompt({
      level: user?.level?.level ?? null,
      levelMaxWords: user?.level?.maxWords ?? null,
      levelDescription: user?.level?.descript ?? null,
      levelGuideline: user?.level?.guideline ?? null,
      context: getOptionalString(payload.context) || null,
      storyPlot: getOptionalString(payload.storyPlot) || null,
      storyDescription: story?.description ?? null,
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
    const modelOverride = getOptionalString(request.body?.model);

    if (!message) {
      response.status(400).json({
        message: "Message is required"
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
      const systemPrompt = await buildSystemPrompt(request.user.id, request.body);
      await historyStore.ensureSystemMessage(request.user.id, systemPrompt);
      const history = await historyStore.load(request.user.id);
      const result = await selectedService.createReply(
        message,
        history,
        modelOverride || undefined
      );

      await historyStore.append(request.user.id, [
        { role: "user", content: message },
        { role: "assistant", content: result.reply }
      ]);

      response.json({
        reply: result.reply,
        model: result.model
      });
    } catch (error) {
      console.error("Error in sendMessage:", error);
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
      const message = kind === "context_update" ? "Context is required" : "Character name is required";
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
      const systemPrompt = await buildSystemPrompt(request.user.id, request.body);
      const prefixWithoutSystem = prefix.filter((message) => message.role !== "system");
      const historyForAI: ChatHistoryMessage[] = [
        { role: "system", content: systemPrompt },
        ...prefixWithoutSystem
      ];

      await historyStore.clear(request.user.id);
      await historyStore.ensureSystemMessage(request.user.id, systemPrompt);

      const result = await selectedService.createReply(
        editedContent,
        historyForAI,
        modelOverride || undefined
      );
      const nextMessages: ChatHistoryMessage[] = [
        ...prefixWithoutSystem,
        { role: "user", content: editedContent },
        { role: "assistant", content: result.reply }
      ];

      await historyStore.append(request.user.id, nextMessages);

      response.json({
        messages: nextMessages.filter((message) => message.role !== "system" && message.role !== "developer"),
        reply: result.reply,
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

      response.json({ activeCharacterNames });
    } catch (error) {
      console.error("Error in getDeveloperState:", error);
      response.status(500).json({
        message: "Failed to load developer state",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return {
    sendMessage,
    getHistory,
    appendDeveloperMessage,
    editMessage,
    getDeveloperState
  };
};
