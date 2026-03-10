import { GoogleGenerativeAI, type ChatSession, type Content, type Part } from "@google/generative-ai";
import { GoogleAIFileManager } from "@google/generative-ai/server";

/**
 * Supported Gemini model identifiers.
 */
export const GEMINI_MODELS = [
  "gemini-2.5-flash",
  "gemini-2.5-pro",
  "gemini-3-flash-preview",
  "gemini-3-pro-preview"
] as const;

export type GeminiModel = (typeof GEMINI_MODELS)[number];

/**
 * Checks if a model string is a Gemini model.
 *
 * @param model - Model identifier string.
 * @returns True if the model is a Gemini model.
 */
export const isGeminiModel = (model: string): boolean => {
  return GEMINI_MODELS.includes(model as GeminiModel);
};

export interface GeminiChatServiceConfig {
  apiKey: string;
  model: string;
}

/**
 * Inline audio attachment to include in a Gemini message.
 */
export interface GeminiAudioPart {
  /** Base64-encoded audio data (without data URL prefix). */
  data: string;
  /** MIME type of the audio (e.g. "audio/wav", "audio/webm"). */
  mimeType: string;
}

export interface GeminiChatService {
  createReply: (
    message?: string,
    history?: Array<{ role: "system" | "developer" | "user" | "assistant"; content: string }>,
    modelOverride?: string,
    audioParts?: GeminiAudioPart[],
    sessionKey?: string
  ) => Promise<{ reply: string; model: string }>;

  /** Removes the cached chat session for the given key so the next call rebuilds from history. */
  clearSession: (sessionKey: string) => void;
}

/**
 * Formats a merged developer+user block for Gemini.
 *
 * When multiple developer messages appear before a user message, they are merged
 * into one user role entry so Gemini receives a single coherent instruction block.
 *
 * @param developerMessages - Developer messages collected before the user message.
 * @param userContent - The user message content to append.
 * @returns Merged message in the form:
 * developer:
 * ...
 * user:
 * ...
 */
const formatMergedDeveloperUserMessage = (developerMessages: string[], userContent: string): string => {
  const developerSections = developerMessages
    .map((entry) => entry.trim())
    .filter((entry) => entry.length > 0)
    .map((entry) => `developer:\n${entry}`);

  const sections = [...developerSections];
  const trimmedUserContent = userContent.trim();

  if (trimmedUserContent.length > 0) {
    sections.push(`user:\n${trimmedUserContent}`);
  }

  return sections.join("\n\n").trim();
};

/**
 * Converts chat history messages to Gemini-compatible format.
 * 
 * Gemini supports only "user" and "model" roles, so we need to:
 * - Convert "assistant" -> "model"
 * - Merge consecutive "developer" messages into one "user" message
 * - If a user message follows those developer messages, append it in the same message block
 * - Skip "system" messages (handled separately as system instruction)
 *
 * @param history - Original chat history with various roles.
 * @returns Gemini-compatible content array.
 */
const convertHistoryToGeminiFormat = (
  history: Array<{ role: "system" | "developer" | "user" | "assistant"; content: string }>
): Content[] => {
  const geminiHistory: Content[] = [];
  const pendingDeveloperMessages: string[] = [];

  const flushDeveloperOnlyBlock = () => {
    if (pendingDeveloperMessages.length === 0) {
      return;
    }

    const mergedDeveloperOnly = formatMergedDeveloperUserMessage(pendingDeveloperMessages, "");
    if (mergedDeveloperOnly) {
      geminiHistory.push({
        role: "user",
        parts: [{ text: mergedDeveloperOnly }]
      });
    }

    pendingDeveloperMessages.length = 0;
  };

  for (const message of history) {
    // Skip system messages - they're handled as systemInstruction
    if (message.role === "system") {
      continue;
    }

    if (message.role === "developer") {
      pendingDeveloperMessages.push(message.content);
    } else if (message.role === "assistant") {
      flushDeveloperOnlyBlock();
      geminiHistory.push({
        role: "model",
        parts: [{ text: message.content }]
      });
    } else if (message.role === "user") {
      if (pendingDeveloperMessages.length > 0) {
        const mergedContent = formatMergedDeveloperUserMessage(pendingDeveloperMessages, message.content);
        if (mergedContent) {
          geminiHistory.push({
            role: "user",
            parts: [{ text: mergedContent }]
          });
        }

        pendingDeveloperMessages.length = 0;
        continue;
      }

      geminiHistory.push({
        role: "user",
        parts: [{ text: message.content }]
      });
    }
  }

  flushDeveloperOnlyBlock();

  return geminiHistory;
};

/**
 * Builds the system instruction for Gemini with developer role explanation.
 *
 * @param systemPrompt - Original system prompt content.
 * @returns Enhanced system instruction with developer role explanation.
 */
const buildGeminiSystemInstruction = (systemPrompt: string): string => {
  const developerRoleExplanation = `
====================================
DEVELOPER ROLE EXPLANATION (GEMINI SPECIFIC)
====================================
You may receive a USER message that contains one or more blocks formatted like this:

developer:
<instruction text>

developer:
<instruction text>

user:
<actual user message>

How this format works:
1. Every "developer:" block is META-LEVEL instruction, not end-user dialogue.
2. The final "user:" block is the real user message you should answer.
3. If there is no "user:" block, treat the content as context update only.

Developer instructions can:
1. Provide context updates (e.g., story progress, relationship changes)
2. Announce character additions or removals
3. Request conversation summaries
4. Provide editing instructions for previous messages

When "developer:" blocks are present:
- DO NOT answer or quote developer text directly
- Apply those instructions silently as constraints/context
- Answer only the "user:" part naturally
- Never expose internal reasoning about these instructions

Example:
- If developer says "Character 'Mimi' has been added", include that character in later responses
- If developer says "Character 'Mimi' has been removed", stop using that character
- If developer provides context updates, incorporate them before answering user

`;

  return developerRoleExplanation + systemPrompt;
};

/**
 * Creates a Gemini chat service for generating AI responses.
 *
 * @param config - Gemini service configuration.
 * @returns Gemini chat service helpers.
 */
export const createGeminiChatService = (config: GeminiChatServiceConfig): GeminiChatService => {
  const genAI = new GoogleGenerativeAI(config.apiKey);
  const fileManager = new GoogleAIFileManager(config.apiKey);
  const defaultModel = config.model;

  /** In-memory cache of active Gemini ChatSession instances keyed by sessionKey. */
  const sessionCache = new Map<string, ChatSession>();

  const clearSession: GeminiChatService["clearSession"] = (sessionKey) => {
    sessionCache.delete(sessionKey);
  };

  const createReply: GeminiChatService["createReply"] = async (message, history = [], modelOverride, audioParts, sessionKey) => {
    const resolvedModel = modelOverride?.trim() || defaultModel;
    
    // Find system message from history
    const systemMessage = history.find((entry) => entry.role === "system");
    const systemPrompt = systemMessage?.content ?? "";
    
    // Build enhanced system instruction with developer role explanation
    const systemInstruction = buildGeminiSystemInstruction(systemPrompt);
    console.log("Gemini system instruction:", systemInstruction);
    
    // Convert history to Gemini format (excluding system messages)
    const geminiHistory = convertHistoryToGeminiFormat(history);
    
    // Get the generative model with system instruction
    const model = genAI.getGenerativeModel({
      model: resolvedModel,
      systemInstruction: systemInstruction
    });

    // Reuse cached ChatSession when available; otherwise create from history
    let chat: ChatSession;
    if (sessionKey && sessionCache.has(sessionKey)) {
      chat = sessionCache.get(sessionKey)!;
    } else {
      chat = model.startChat({ history: geminiHistory });
      if (sessionKey) {
        sessionCache.set(sessionKey, chat);
      }
      console.log("Started new Gemini chat session with history. Session key:", sessionKey);
    }

    // Send the user message if provided
    let userMessage = message?.trim() ?? "";

    // When reusing a cached session, any developer messages appended after the
    // last user/assistant turn haven't been sent to the session yet.  Merge them
    // into the current user message so the AI receives the context inline.
    if (sessionKey && sessionCache.has(sessionKey)) {
      const trailingDevMessages: string[] = [];
      for (let i = history.length - 1; i >= 0; i--) {
        if (history[i].role === "developer") {
          trailingDevMessages.unshift(history[i].content);
        } else {
          break;
        }
      }

      if (trailingDevMessages.length > 0) {
        userMessage = formatMergedDeveloperUserMessage(trailingDevMessages, userMessage);
      }
    }
    
    if (!userMessage && geminiHistory.length === 0 && (!audioParts || audioParts.length === 0)) {
      throw new Error("No message or history provided for Gemini");
    }

    // Upload audio files via Google AI File API and build message parts
    const messageParts: Part[] = [];

    if (audioParts && audioParts.length > 0) {
      for (const audio of audioParts) {
        const buffer = Buffer.from(audio.data, "base64");
        const uploadResult = await fileManager.uploadFile(buffer, {
          mimeType: audio.mimeType,
          displayName: `user-audio-${Date.now()}`
        });

        messageParts.push({
          fileData: {
            fileUri: uploadResult.file.uri,
            mimeType: uploadResult.file.mimeType
          }
        });
      }
    }

    messageParts.push({ text: userMessage || "Continue the conversation." });

    const result = await chat.sendMessage(messageParts);
    const response = result.response;
    const reply = response.text().trim();

    if (process.env.NODE_ENV !== "production") {
      console.log("Gemini history:", geminiHistory);
      console.log("Gemini response reply:", reply);
    }

    if (!reply) {
      throw new Error("Gemini returned an empty response");
    }

    return {
      reply,
      model: resolvedModel
    };
  };

  return { createReply, clearSession };
};
