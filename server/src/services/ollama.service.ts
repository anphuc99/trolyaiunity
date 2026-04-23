import OpenAI from "openai";

/**
 * Configuration for the Ollama chat service.
 */
export interface OllamaChatServiceConfig {
  /** Ollama server URL (default: http://localhost:11434). */
  baseUrl: string;
  /** Ollama model name (e.g. "gemma4:e4b"). */
  model: string;
}

/**
 * Ollama chat service interface — matches OpenAIChatService shape
 * so it can be used as a drop-in replacement in the chat controller.
 */
export interface OllamaChatService {
  createReply: (
    message?: string,
    history?: Array<{ role: "system" | "developer" | "user" | "assistant"; content: string }>,
    modelOverride?: string
  ) => Promise<{ reply: string; model: string }>;
}

/**
 * Creates an Ollama chat service using the OpenAI-compatible API.
 *
 * Ollama exposes an OpenAI-compatible endpoint at /v1/chat/completions.
 * We use the OpenAI SDK with a custom baseURL to communicate with it.
 *
 * @param config - Ollama service configuration.
 * @returns Ollama chat service helpers.
 */
export const createOllamaChatService = (config: OllamaChatServiceConfig): OllamaChatService => {
  const client = new OpenAI({
    baseURL: `${config.baseUrl}/v1`,
    apiKey: "ollama", // Ollama doesn't need a real API key
  });
  const defaultModel = config.model;

  const createReply: OllamaChatService["createReply"] = async (message, history = [], modelOverride) => {
    const resolvedModel = modelOverride?.trim() || defaultModel;

    // Build messages array from history
    const messages: Array<{ role: "system" | "user" | "assistant"; content: string }> = [];

    for (const entry of history) {
      if (entry.role === "system") {
        messages.push({ role: "system", content: entry.content });
      } else if (entry.role === "developer") {
        // Merge developer messages as system context
        messages.push({ role: "system", content: `[Developer instruction]: ${entry.content}` });
      } else if (entry.role === "user" || entry.role === "assistant") {
        messages.push({ role: entry.role, content: entry.content });
      }
    }

    // Add the current user message
    if (message) {
      messages.push({ role: "user", content: message });
    }

    console.log(`[Ollama] Sending to ${resolvedModel} — messages: ${messages.length}`);

    const response = await client.chat.completions.create({
      model: resolvedModel,
      messages,
    });

    const reply = response.choices?.[0]?.message?.content?.trim() ?? "";

    if (process.env.NODE_ENV !== "production") {
      console.log("[Ollama] Response:", reply.slice(0, 500));
    }

    if (!reply) {
      throw new Error("Ollama returned an empty response");
    }

    return {
      reply,
      model: response.model ?? resolvedModel,
    };
  };

  return { createReply };
};
