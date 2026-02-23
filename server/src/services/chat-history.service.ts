import fs from "fs/promises";
import path from "path";

let hasLoggedChatHistoryParseFailure = false;

export type ChatHistoryRole = "system" | "developer" | "user" | "assistant";

export interface ChatHistoryMessage {
  role: ChatHistoryRole;
  content: string;
}

export interface ChatHistoryStore {
  load: (userId: number) => Promise<ChatHistoryMessage[]>;
  append: (userId: number, messages: ChatHistoryMessage[]) => Promise<void>;
  ensureSystemMessage: (userId: number, content: string) => Promise<void>;
  clear: (userId: number) => Promise<void>;
}

const DEFAULT_DIR = path.join(process.cwd(), "data", "chat-history");

/**
 * Validates and sanitizes the user ID.
 *
 * @param userId - User ID to validate.
 * @returns Sanitized user ID as a string.
 */
const sanitizeUserId = (userId: number) => {
  if (!Number.isInteger(userId) || userId <= 0) {
    throw new Error("Invalid userId");
  }

  return String(userId);
};

/**
 * Generates the history file path for a user.
 *
 * @param dir - Directory to store history files.
 * @param userId - User ID.
 * @returns Absolute path to the history file.
 */
const toHistoryPath = (dir: string, userId: number) => {
  const safeUserId = sanitizeUserId(userId);
  return path.join(dir, `${safeUserId}.history.txt`);
};

/**
 * Parses raw history file content into ChatHistoryMessage array.
 *
 * @param raw - Raw file contents (newline-delimited JSON).
 * @returns Array of parsed messages.
 */
const parseHistoryRaw = (raw: string) =>
  raw
    .split(/\r?\n/g)
    .map(parseLine)
    .filter((value): value is ChatHistoryMessage => Boolean(value));

/**
 * Parses a single line of the history file.
 *
 * @param line - Single line from the history file.
 * @returns Parsed message or null if invalid.
 */
const parseLine = (line: string): ChatHistoryMessage | null => {
  const trimmed = line.trim();

  if (!trimmed) {
    return null;
  }

  try {
    const parsed = JSON.parse(trimmed) as { role?: unknown; content?: unknown };

    if (
      (parsed.role !== "system" && parsed.role !== "developer" && parsed.role !== "user" && parsed.role !== "assistant") ||
      typeof parsed.content !== "string"
    ) {
      return null;
    }

    const content = parsed.content.trim();

    if (!content) {
      return null;
    }

    return { role: parsed.role, content };
  } catch (error) {
    if (!hasLoggedChatHistoryParseFailure) {
      console.warn("Failed to parse chat history line; ignoring invalid entry.", error);
      hasLoggedChatHistoryParseFailure = true;
    }
    return null;
  }
};

/**
 * Creates a file-backed chat history store.
 *
 * History format: newline-delimited JSON objects written to a .txt file.
 * This keeps message ordering stable so the OpenAI prompt prefix is identical
 * across requests and server restarts (which helps prompt caching).
 *
 * Storage is per-user (not per-session) to prevent losing history when session changes.
 *
 * @param dir - Directory to store history files in.
 * @returns A chat history store.
 */
export const createChatHistoryStore = (dir: string = process.env.CHAT_HISTORY_DIR ?? DEFAULT_DIR): ChatHistoryStore => {
  /**
   * Loads the chat history for a user.
   *
   * @param userId - Authenticated user id.
   * @returns Array of chat history messages.
   */
  const load: ChatHistoryStore["load"] = async (userId) => {
    const filePath = toHistoryPath(dir, userId);

    try {
      const raw = await fs.readFile(filePath, "utf8");
      return parseHistoryRaw(raw);
    } catch (error) {
      if ((error as NodeJS.ErrnoException)?.code === "ENOENT") {
        return [];
      }

      console.error("Failed to load chat history.", error);
      throw error;
    }
  };

  /**
   * Appends messages to the chat history for a user.
   *
   * @param userId - Authenticated user id.
   * @param messages - Messages to append.
   */
  const append: ChatHistoryStore["append"] = async (userId, messages) => {
    if (!messages.length) {
      return;
    }

    const filePath = toHistoryPath(dir, userId);
    await fs.mkdir(path.dirname(filePath), { recursive: true });

    const lines = messages
      .map((message) => ({ role: message.role, content: message.content }))
      .map((message) => JSON.stringify(message))
      .join("\n");

    await fs.appendFile(filePath, `${lines}\n`, "utf8");
  };

  /**
   * Ensures the chat history starts with the system instruction.
   *
   * @param userId - Authenticated user id.
   * @param content - System instruction text.
   */
  const ensureSystemMessage: ChatHistoryStore["ensureSystemMessage"] = async (userId, content) => {
    const filePath = toHistoryPath(dir, userId);
    const systemContent = content.trim();

    if (!systemContent) {
      return;
    }

    try {
      const raw = await fs.readFile(filePath, "utf8");
      const messages = parseHistoryRaw(raw);

      if (messages.length && messages[0].role === "system" && messages[0].content === systemContent) {
        return;
      }

      const nextMessages = [{ role: "system", content: systemContent }, ...messages];
      const nextRaw = nextMessages.map((message) => JSON.stringify(message)).join("\n");
      await fs.mkdir(path.dirname(filePath), { recursive: true });
      await fs.writeFile(filePath, `${nextRaw}\n`, "utf8");
    } catch (error) {
      if ((error as NodeJS.ErrnoException)?.code === "ENOENT") {
        const systemMessage = JSON.stringify({ role: "system", content: systemContent });
        await fs.mkdir(path.dirname(filePath), { recursive: true });
        await fs.writeFile(filePath, `${systemMessage}\n`, "utf8");
        return;
      }

      console.error("Failed to ensure system chat history message.", error);
      throw error;
    }
  };

  /**
   * Clears the stored history for a user.
   *
   * @param userId - Authenticated user id.
   */
  const clear: ChatHistoryStore["clear"] = async (userId) => {
    const filePath = toHistoryPath(dir, userId);

    try {
      await fs.unlink(filePath);
    } catch (error) {
      if ((error as NodeJS.ErrnoException)?.code === "ENOENT") {
        return;
      }

      console.error("Failed to clear chat history.", error);
      throw error;
    }
  };

  return {
    load,
    append,
    ensureSystemMessage,
    clear
  };
};
