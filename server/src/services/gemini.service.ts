import { GoogleGenerativeAI, type Content, type Part } from "@google/generative-ai";

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

export interface GeminiChatService {
  createReply: (
    message?: string,
    history?: Array<{ role: "system" | "developer" | "user" | "assistant"; content: string }>,
    modelOverride?: string
  ) => Promise<{ reply: string; model: string }>;
}

/**
 * Formats a developer role message for Gemini.
 * 
 * Since Gemini doesn't have a "developer" role, we convert developer messages
 * to user messages with a special prefix explaining the role.
 *
 * @param content - Original developer message content.
 * @returns Formatted content with developer role explanation.
 */
const formatDeveloperMessageForGemini = (content: string): string => {
  return `[DEVELOPER INSTRUCTION - This is a system-level instruction that should be followed but not directly responded to. It provides context updates, character changes, or other meta-information for the conversation.]

${content}

[END DEVELOPER INSTRUCTION - Continue the conversation naturally based on this instruction.]`;
};

/**
 * Converts chat history messages to Gemini-compatible format.
 * 
 * Gemini supports only "user" and "model" roles, so we need to:
 * - Convert "assistant" -> "model"
 * - Convert "developer" -> "user" with special formatting
 * - Skip "system" messages (handled separately as system instruction)
 *
 * @param history - Original chat history with various roles.
 * @returns Gemini-compatible content array.
 */
const convertHistoryToGeminiFormat = (
  history: Array<{ role: "system" | "developer" | "user" | "assistant"; content: string }>
): Content[] => {
  const geminiHistory: Content[] = [];

  for (const message of history) {
    // Skip system messages - they're handled as systemInstruction
    if (message.role === "system") {
      continue;
    }

    if (message.role === "developer") {
      // Convert developer messages to user messages with special formatting
      geminiHistory.push({
        role: "user",
        parts: [{ text: formatDeveloperMessageForGemini(message.content) }]
      });
      
      // Add a placeholder model response to maintain conversation flow
      // This prevents consecutive user messages which Gemini doesn't handle well
      geminiHistory.push({
        role: "model",
        parts: [{ text: "[ACKNOWLEDGED - Instruction received and understood.]" }]
      });
    } else if (message.role === "assistant") {
      geminiHistory.push({
        role: "model",
        parts: [{ text: message.content }]
      });
    } else if (message.role === "user") {
      geminiHistory.push({
        role: "user",
        parts: [{ text: message.content }]
      });
    }
  }

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
During this conversation, you may receive messages prefixed with "[DEVELOPER INSTRUCTION]".
These are META-LEVEL instructions that:
1. Provide context updates (e.g., story progress, relationship changes)
2. Announce character additions or removals
3. Request conversation summaries
4. Provide editing instructions for previous messages

When you see a DEVELOPER INSTRUCTION:
- DO NOT respond to it directly as if it were a user message
- Simply acknowledge it internally with "[ACKNOWLEDGED]" 
- Apply the instruction silently to your subsequent responses
- Continue the conversation naturally based on the new context

Example:
- If a developer instruction says "Character 'Mimi' has been added", start including that character in your responses
- If it says "Character 'Mimi' has been removed", stop using that character
- If it provides context updates, incorporate them into your understanding

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
  const defaultModel = config.model;

  const createReply: GeminiChatService["createReply"] = async (message, history = [], modelOverride) => {
    const resolvedModel = modelOverride?.trim() || defaultModel;
    
    // Find system message from history
    const systemMessage = history.find((entry) => entry.role === "system");
    const systemPrompt = systemMessage?.content ?? "";
    
    // Build enhanced system instruction with developer role explanation
    const systemInstruction = buildGeminiSystemInstruction(systemPrompt);
    
    // Convert history to Gemini format (excluding system messages)
    const geminiHistory = convertHistoryToGeminiFormat(history);
    
    // Get the generative model with system instruction
    const model = genAI.getGenerativeModel({
      model: resolvedModel,
      systemInstruction: systemInstruction
    });

    // Start a chat session with the converted history
    const chat = model.startChat({
      history: geminiHistory
    });

    // Send the user message if provided
    const userMessage = message?.trim() ?? "";
    
    if (!userMessage && geminiHistory.length === 0) {
      throw new Error("No message or history provided for Gemini");
    }

    const result = await chat.sendMessage(userMessage || "Continue the conversation.");
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

  return { createReply };
};
