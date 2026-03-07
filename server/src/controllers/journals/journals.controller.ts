import type { Request, Response } from "express";
import type { DataSource } from "typeorm";
import { Like } from "typeorm";
import JournalEntity from "../../models/journal.entity.js";
import MessageEntity from "../../models/message.entity.js";
import CharacterEntity from "../../models/character.entity.js";
import { createOpenAIChatService, type OpenAIChatService } from "../../services/openai.service.js";
import { createChatHistoryStore, type ChatHistoryMessage, type ChatHistoryStore } from "../../services/chat-history.service.js";

interface JournalController {
  listJournals: (request: Request, response: Response) => Promise<void>;
  getJournal: (request: Request, response: Response) => Promise<void>;
  searchMessages: (request: Request, response: Response) => Promise<void>;
  endConversation: (request: Request, response: Response) => Promise<void>;
}

interface JournalControllerDeps {
  openAIService?: OpenAIChatService;
  historyStore?: ChatHistoryStore;
}

interface AssistantTurn {
  MessageId?: string;
  CharacterName?: string;
  Text?: string;
  Tone?: string;
  Translation?: string;
}

const parseId = (value: string) => {
  const id = Number.parseInt(value, 10);
  return Number.isNaN(id) ? null : id;
};

let hasLoggedAssistantReplyParseFailure = false;

/**
 * Attempts to parse an assistant reply as JSON array of turns.
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
        console.warn("Failed to parse assistant reply as JSON; attempting fallback extraction.", error);
        hasLoggedAssistantReplyParseFailure = true;
      }
      return null;
    }
    return null;
  };

  const direct = tryParse(trimmed);
  if (direct) return direct;

  // Try extracting JSON array from within the text
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

/**
 * Builds the Journal controller with injected data source dependencies.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @param deps - Optional overrides for external services.
 * @returns The Journal controller handlers.
 */
export const createJournalController = (
  dataSource: DataSource,
  deps: JournalControllerDeps = {}
): JournalController => {
  const journalRepository = dataSource.getRepository(JournalEntity);
  const messageRepository = dataSource.getRepository(MessageEntity);
  const characterRepository = dataSource.getRepository(CharacterEntity);
  const apiKey = process.env.OPENAI_API_KEY ?? "";
  const model = process.env.OPENAI_MODEL ?? "gpt-4.1-mini";
  const systemPromptPath = process.env.OPENAI_SYSTEM_PROMPT_PATH;
  const openAIService =
    deps.openAIService ?? (apiKey ? createOpenAIChatService({ apiKey, model, systemPromptPath }) : null);
  const historyStore = deps.historyStore ?? createChatHistoryStore();

  /**
   * Lists all journals for the authenticated user.
   */
  const listJournals: JournalController["listJournals"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const journals = await journalRepository.find({
        where: { userId: request.user.id },
        relations: ["character"],
        order: { createdAt: "DESC" }
      });

      const result = journals.map((journal) => ({
        id: journal.id,
        summary: journal.summary,
        characterId: journal.characterId ?? null,
        characterName: journal.character?.name ?? null,
        createdAt: journal.createdAt.toISOString()
      }));

      response.json(result);
    } catch (error) {
      console.error("Failed to load journals.", error);
      response.status(500).json({
        message: "Failed to load journals",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Returns a single journal with all its messages.
   */
  const getJournal: JournalController["getJournal"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const id = parseId(request.params.id);
    if (!id) {
      response.status(400).json({ message: "Invalid journal ID" });
      return;
    }

    try {
      const journal = await journalRepository.findOne({
        where: { id, userId: request.user.id },
        relations: ["character"]
      });

      if (!journal) {
        response.status(404).json({ message: "Journal not found" });
        return;
      }

      const messages = await messageRepository.find({
        where: { journalId: id, userId: request.user.id },
        order: { createdAt: "ASC" }
      });

      response.json({
        id: journal.id,
        summary: journal.summary,
        characterId: journal.characterId ?? null,
        characterName: journal.character?.name ?? null,
        createdAt: journal.createdAt.toISOString(),
        messages: messages.map((m) => ({
          id: m.id,
          content: m.content,
          characterName: m.characterName,
          translation: m.translation ?? null,
          tone: m.tone ?? null,
          audio: m.audio ?? null,
          createdAt: m.createdAt.toISOString()
        }))
      });
    } catch (error) {
      console.error("Failed to load journal.", error);
      response.status(500).json({
        message: "Failed to load journal",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Searches messages by content keyword.
   */
  const searchMessages: JournalController["searchMessages"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const query = typeof request.query.q === "string" ? request.query.q.trim() : "";
    if (!query) {
      response.status(400).json({ message: "Search query (q) is required" });
      return;
    }

    try {
      const messages = await messageRepository.find({
        where: {
          userId: request.user.id,
          content: Like(`%${query}%`)
        },
        order: { createdAt: "DESC" },
        take: 50
      });

      response.json(
        messages.map((m) => ({
          id: m.id,
          content: m.content,
          characterName: m.characterName,
          journalId: m.journalId,
          translation: m.translation ?? null,
          tone: m.tone ?? null,
          createdAt: m.createdAt.toISOString()
        }))
      );
    } catch (error) {
      console.error("Failed to search messages.", error);
      response.status(500).json({
        message: "Failed to search messages",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Ends a conversation: summarizes via AI, saves messages + journal, clears history.
   *
   * Body: { characterId?: number }
   */
  const endConversation: JournalController["endConversation"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const body = request.body as Record<string, unknown>;
    const characterId = typeof body.characterId === "number" ? body.characterId : null;

    try {
      const history = await historyStore.load(request.user.id);
      const nonSystemHistory = history.filter((m) => m.role !== "system" && m.role !== "developer");

      if (nonSystemHistory.length === 0) {
        response.status(400).json({ message: "No conversation to save" });
        return;
      }

      // Resolve character
      let character: CharacterEntity | null = null;
      if (characterId) {
        character = await characterRepository.findOne({
          where: { id: characterId, userId: request.user.id }
        });
      }

      // Generate summary via AI (or fallback to simple summary)
      let summary = "Conversation summary";
      if (openAIService) {
        try {
          const summaryPrompt =
            "Summarize the following conversation in 2-3 sentences. Focus on the key topics discussed and any learning outcomes.";
          const conversationText = nonSystemHistory
            .map((m) => `${m.role}: ${m.content}`)
            .join("\n");

          const result = await openAIService.createReply(
            `${summaryPrompt}\n\n${conversationText}`,
            [],
            undefined
          );
          summary = result.reply;
        } catch (summaryError) {
          console.warn("Failed to generate AI summary, using fallback.", summaryError);
          summary = `Conversation with ${nonSystemHistory.length} messages`;
        }
      } else {
        summary = `Conversation with ${nonSystemHistory.length} messages`;
      }

      // Create journal
      const journal = journalRepository.create({
        summary,
        characterId: character?.id ?? null,
        userId: request.user.id
      });

      const savedJournal = await journalRepository.save(journal);

      // Save messages from history
      const characterName = character?.name ?? "Tutor";
      const messageEntities: MessageEntity[] = [];

      for (const historyMessage of nonSystemHistory) {
        if (historyMessage.role === "user") {
          const msg = messageRepository.create({
            content: historyMessage.content,
            characterName: "user",
            userId: request.user.id,
            journalId: savedJournal.id
          });
          messageEntities.push(msg);
        } else if (historyMessage.role === "assistant") {
          // Try parse structured JSON reply
          const turns = parseAssistantReply(historyMessage.content);

          if (turns.length > 0) {
            for (const turn of turns) {
              const msg = messageRepository.create({
                id: typeof turn.MessageId === "string" ? turn.MessageId : undefined,
                content: typeof turn.Text === "string" ? turn.Text : historyMessage.content,
                characterName: typeof turn.CharacterName === "string" ? turn.CharacterName : characterName,
                translation: typeof turn.Translation === "string" ? turn.Translation : null,
                tone: typeof turn.Tone === "string" ? turn.Tone : null,
                userId: request.user.id,
                journalId: savedJournal.id
              });
              messageEntities.push(msg);
            }
          } else {
            // Fallback: save raw content
            const msg = messageRepository.create({
              content: historyMessage.content,
              characterName,
              userId: request.user.id,
              journalId: savedJournal.id
            });
            messageEntities.push(msg);
          }
        }
      }

      if (messageEntities.length > 0) {
        await messageRepository.save(messageEntities);
      }

      // Clear chat history
      await historyStore.clear(request.user.id);

      response.json({
        id: savedJournal.id,
        summary: savedJournal.summary,
        characterId: savedJournal.characterId ?? null,
        characterName: character?.name ?? null,
        messageCount: messageEntities.length,
        createdAt: savedJournal.createdAt.toISOString()
      });
    } catch (error) {
      console.error("Failed to end conversation.", error);
      response.status(500).json({
        message: "Failed to end conversation",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return { listJournals, getJournal, searchMessages, endConversation };
};
