import fsSync from "fs";
import path from "path";
import https from "https";
import OpenAI from "openai";

/**
 * Default system prompt for the MimiLearn AI tutor.
 */
const DEFAULT_SYSTEM_PROMPT = `YOU ARE AN AI LEARNING TUTOR.

====================================
ABSOLUTE RULES (SYSTEM CRITICAL)
====================================
1. Be helpful and encouraging.
2. Explain concepts clearly and simply.
3. Adapt to the user's learning level.

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

====================================
FINAL CHECK
====================================
Silently verify all ABSOLUTE RULES before responding.`;

export interface OpenAIChatServiceConfig {
  apiKey: string;
  model: string;
  customSystemPrompt?: string;
  systemPromptPath?: string;
  tlsCaCertPath?: string;
  tlsCaCertBase64?: string;
  tlsAllowInsecure?: boolean;
}

export interface OpenAIClientTlsOptions {
  tlsCaCertPath?: string;
  tlsCaCertBase64?: string;
  tlsAllowInsecure?: boolean;
}

const parseEnvBool = (value: string | undefined) => {
  const normalized = String(value ?? "").trim().toLowerCase();
  if (!normalized) return null;
  if (["1", "true", "yes", "y", "on"].includes(normalized)) return true;
  if (["0", "false", "no", "n", "off"].includes(normalized)) return false;
  return null;
};

const isDevMode = () => {
  const nodeEnv = String(process.env.NODE_ENV ?? "").trim().toLowerCase();
  const lifecycle = String(process.env.npm_lifecycle_event ?? "").trim().toLowerCase();
  return nodeEnv === "development" || lifecycle === "dev";
};

/**
 * Builds an HTTPS agent with optional custom CA bundle.
 */
const buildOpenAIHttpAgent = (config: {
  tlsCaCertPath?: string;
  tlsCaCertBase64?: string;
  tlsAllowInsecure?: boolean;
}) => {
  const caFromBase64 = typeof config.tlsCaCertBase64 === "string" ? config.tlsCaCertBase64.trim() : "";
  const caFromPath = typeof config.tlsCaCertPath === "string" ? config.tlsCaCertPath.trim() : "";

  const resolvedCa = (() => {
    if (caFromBase64) {
      try {
        return Buffer.from(caFromBase64, "base64").toString("utf8");
      } catch (error) {
        console.error("Failed to decode OPENAI_TLS_CA_CERT_BASE64.", error);
        throw new Error("OPENAI_TLS_CA_CERT_BASE64 is not valid base64");
      }
    }

    if (caFromPath) {
      const resolvedPath = path.isAbsolute(caFromPath) ? caFromPath : path.resolve(process.cwd(), caFromPath);
      if (!fsSync.existsSync(resolvedPath)) {
        throw new Error(`OPENAI_TLS_CA_CERT_PATH not found: ${resolvedPath}`);
      }
      return fsSync.readFileSync(resolvedPath, "utf8");
    }

    return "";
  })();

  const allowInsecure = Boolean(config.tlsAllowInsecure);

  if (!allowInsecure && !resolvedCa) {
    return undefined;
  }

  return new https.Agent({
    keepAlive: true,
    rejectUnauthorized: !allowInsecure,
    ...(resolvedCa ? { ca: resolvedCa } : {})
  });
};

/**
 * Creates an OpenAI SDK client configured for optional corporate proxy TLS.
 *
 * @param apiKey - OpenAI API key.
 * @param options - Optional TLS overrides.
 * @returns A configured OpenAI client.
 */
export const createOpenAIClient = (apiKey: string, options: OpenAIClientTlsOptions = {}) => {
  const envInsecure = parseEnvBool(process.env.OPENAI_TLS_INSECURE);
  const tlsAllowInsecure = options.tlsAllowInsecure ?? envInsecure ?? isDevMode();

  const tlsCaCertPath = options.tlsCaCertPath ?? process.env.OPENAI_TLS_CA_CERT_PATH;
  const tlsCaCertBase64 = options.tlsCaCertBase64 ?? process.env.OPENAI_TLS_CA_CERT_BASE64;

  const httpAgent = buildOpenAIHttpAgent({
    tlsAllowInsecure,
    tlsCaCertPath,
    tlsCaCertBase64
  });

  return new OpenAI({ apiKey, ...(httpAgent ? { httpAgent } : {}) });
};

export interface OpenAIChatService {
  createReply: (
    message?: string,
    history?: Array<{ role: "system" | "developer" | "user" | "assistant"; content: string }>,
    modelOverride?: string
  ) => Promise<{ reply: string; model: string }>;
}

/**
 * Creates a lightweight OpenAI chat service with an embedded default system prompt.
 *
 * @param config - OpenAI service configuration.
 * @returns OpenAI chat service helpers.
 */
export const createOpenAIChatService = (config: OpenAIChatServiceConfig): OpenAIChatService => {
  const client = createOpenAIClient(config.apiKey, {
    tlsAllowInsecure: config.tlsAllowInsecure,
    tlsCaCertPath: config.tlsCaCertPath,
    tlsCaCertBase64: config.tlsCaCertBase64
  });
  const model = config.model;

  const systemPrompt = (() => {
    if (config.customSystemPrompt?.trim()) {
      return config.customSystemPrompt.trim();
    }

    if (config.systemPromptPath?.trim()) {
      try {
        const resolvedPath = path.resolve(process.cwd(), config.systemPromptPath.trim());
        if (fsSync.existsSync(resolvedPath)) {
          return fsSync.readFileSync(resolvedPath, "utf8").trim();
        }
      } catch (caught) {
        console.warn(`Failed to read system prompt from path: ${config.systemPromptPath}`, caught);
      }
    }

    return DEFAULT_SYSTEM_PROMPT;
  })();

  const createReply: OpenAIChatService["createReply"] = async (message, history = [], modelOverride) => {
    const normalizedHistory = history
      .filter(
        (entry) =>
          entry && (entry.role === "system" || entry.role === "developer" || entry.role === "user" || entry.role === "assistant")
      )
      .map((entry) => ({ role: entry.role, content: entry.content }));

    const hasSystemMessage = normalizedHistory.find((entry) => entry.role === "system");
    const effectiveSystemPrompt = hasSystemMessage ? hasSystemMessage.content : systemPrompt;
    const messages = normalizedHistory.filter((entry) => entry.role !== "system");

    if (message !== undefined) {
      messages.push({ role: "user", content: message });
    }
    const resolvedModel = modelOverride?.trim() || model;

    const response = await client.responses.create({
      model: resolvedModel,
      instructions: effectiveSystemPrompt,
      input: [...messages]
    });

    const reply = response.output_text?.trim() ?? "";

    if (process.env.NODE_ENV !== "production") {
      console.log(messages);
      console.log("OpenAI response reply:", reply);
    }

    if (!reply) {
      throw new Error("OpenAI returned an empty response");
    }

    return {
      reply,
      model: response.model ?? resolvedModel
    };
  };

  return { createReply };
};
