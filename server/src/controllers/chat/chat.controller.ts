import type { Request, Response } from "express";
import type { DataSource } from "typeorm";
import { createOpenAIChatService, type OpenAIChatService } from "../../services/openai.service.js";
import { createGeminiChatService, isGeminiModel, type GeminiChatService } from "../../services/gemini.service.js";
import { createChatHistoryStore, type ChatHistoryMessage, type ChatHistoryStore } from "../../services/chat-history.service.js";
import CharacterEntity from "../../models/character.entity.js";

interface ChatController {
  sendMessage: (request: Request, response: Response) => Promise<void>;
  getHistory: (request: Request, response: Response) => Promise<void>;
  clearHistory: (request: Request, response: Response) => Promise<void>;
}

interface ChatControllerDeps {
  openAIService?: OpenAIChatService;
  geminiService?: GeminiChatService;
  historyStore?: ChatHistoryStore;
}

type ChatReplyService = OpenAIChatService | GeminiChatService;

/**
 * Builds the system prompt with character context.
 */
const buildSystemPrompt = (character: CharacterEntity | null): string => {
  let prompt = `YOU ARE AN AI LEARNING TUTOR FOR THE MIMILEARN APP.

====================================
ABSOLUTE RULES (SYSTEM CRITICAL)
====================================
1. Be helpful and encouraging.
2. Explain concepts clearly and simply.
3. Adapt to the user's learning level.`;

  if (character) {
    prompt += `
4. Stay in character as "${character.name}".

====================================
CHARACTER INFO
====================================
Name: ${character.name}
${character.age ? `Age: ${character.age}` : ""}
Personality: ${character.personality}`;
  }

  prompt += `

====================================
TEACHING STYLE
====================================
- Break down complex topics into simple steps.
- Use examples and analogies when helpful.
- Ask follow-up questions to check understanding.
- Provide positive reinforcement.

====================================
DIALOGUE RULES
====================================
- Keep responses focused and concise.
- If the user is confused, try a different explanation approach.
- Encourage the user to think through problems.
- Prefer 1-3 paragraphs per reply unless a longer explanation is needed.

====================================
RESPONSE FORMAT
====================================
Respond in JSON array format. Each element is an object with these fields:
- "MessageId": a unique UUID string
- "CharacterName": the character's name (or "Tutor" if no character)
- "Text": the reply text
- "Tone": emotional tone (e.g. "friendly", "encouraging", "curious")
- "Translation": optional translation (null if not needed)

Example:
[{"MessageId":"abc-123","CharacterName":"${character?.name ?? "Tutor"}","Text":"Hello! How can I help you learn today?","Tone":"friendly","Translation":null}]

====================================
FINAL CHECK
====================================
Silently verify all ABSOLUTE RULES before responding.`;

  return prompt;
};

/**
 * Builds the Chat controller with injected data source dependencies.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @param deps - Optional overrides for external services.
 * @returns The Chat controller handlers.
 */
export const createChatController = (
  dataSource: DataSource,
  deps: ChatControllerDeps = {}
): ChatController => {
  const characterRepository = dataSource.getRepository(CharacterEntity);

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

  /**
   * Resolves the AI service to use based on model name.
   */
  const resolveService = (modelOverride?: string): ChatReplyService | null => {
    if (modelOverride && isGeminiModel(modelOverride)) {
      return geminiService;
    }
    return openAIService ?? geminiService;
  };

  /**
   * Sends a user message and returns the AI reply.
   *
   * Body: { message: string, characterId?: number, model?: string, context?: string }
   */
  const sendMessage: ChatController["sendMessage"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const body = request.body as Record<string, unknown>;
    const message = typeof body.message === "string" ? body.message.trim() : "";
    const characterId = typeof body.characterId === "number" ? body.characterId : null;
    const modelOverride = typeof body.model === "string" ? body.model.trim() : undefined;
    const context = typeof body.context === "string" ? body.context.trim() : "";

    if (!message) {
      response.status(400).json({ message: "Message content is required" });
      return;
    }

    const service = resolveService(modelOverride);
    if (!service) {
      response.status(503).json({ message: "No AI service configured. Set OPENAI_API_KEY or GOOGLE_API_KEY." });
      return;
    }

    try {
      // Load character if specified
      let character: CharacterEntity | null = null;
      if (characterId) {
        character = await characterRepository.findOne({
          where: { id: characterId, userId: request.user.id }
        });
      }

      // Build system prompt with character context
      const systemPrompt = buildSystemPrompt(character);

      // Ensure system message in history
      await historyStore.ensureSystemMessage(request.user.id, systemPrompt);

      // Load history and add context as developer message if provided
      if (context) {
        await historyStore.append(request.user.id, [{ role: "developer", content: context }]);
      }

      const history = await historyStore.load(request.user.id);

      // Call AI service
      const result = await service.createReply(message, history, modelOverride);

      // Append user message and assistant reply to history
      await historyStore.append(request.user.id, [
        { role: "user", content: message },
        { role: "assistant", content: result.reply }
      ]);

      response.json({
        reply: result.reply,
        model: result.model
      });
    } catch (error) {
      console.error("Failed to send message.", error);
      response.status(500).json({
        message: "Failed to generate AI response",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Returns the full chat history for the authenticated user.
   */
  const getHistory: ChatController["getHistory"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const history = await historyStore.load(request.user.id);
      response.json(history);
    } catch (error) {
      console.error("Failed to load chat history.", error);
      response.status(500).json({
        message: "Failed to load chat history",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Clears the chat history for the authenticated user.
   */
  const clearHistory: ChatController["clearHistory"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      await historyStore.clear(request.user.id);
      response.json({ message: "Chat history cleared" });
    } catch (error) {
      console.error("Failed to clear chat history.", error);
      response.status(500).json({
        message: "Failed to clear chat history",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return { sendMessage, getHistory, clearHistory };
};
