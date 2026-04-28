import type { Request, Response } from "express";
import { execFile } from "child_process";
import fs from "fs/promises";
import path from "path";
import crypto from "crypto";
import ffmpegInstaller from "@ffmpeg-installer/ffmpeg";
import type { DataSource } from "typeorm";
import { Like } from "typeorm";
import JournalEntity from "../../models/journal.entity.js";
import JournalReviewEntity from "../../models/journal-review.entity.js";
import MessageEntity from "../../models/message.entity.js";
import CharacterEntity from "../../models/character.entity.js";
import StoryEntity from "../../models/story.entity.js";
import UserEntity from "../../models/user.entity.js";
import { createOpenAIChatService, type OpenAIChatService } from "../../services/openai.service.js";
import { createChatHistoryStore, type ChatHistoryMessage, type ChatHistoryStore } from "../../services/chat-history.service.js";
import { buildAudioId, getAudioPath, createTtsAudio, createGeminiTtsAudio } from "../../services/tts.service.js";
import {
  createInitialReviewState,
  updateReviewAfterRating,
  type FSRSRating,
  type ReviewHistoryEntry,
  type ReviewState
} from "../../services/fsrs.service.js";

interface JournalController {
  listJournals: (request: Request, response: Response) => Promise<void>;
  getJournal: (request: Request, response: Response) => Promise<void>;
  searchMessages: (request: Request, response: Response) => Promise<void>;
  endConversation: (request: Request, response: Response) => Promise<void>;
  /** Accepts a pre-computed summary from the client (local AI) and saves journal without server-side AI call. */
  endConversationLocal: (request: Request, response: Response) => Promise<void>;
  getDueJournals: (request: Request, response: Response) => Promise<void>;
  submitJournalReview: (request: Request, response: Response) => Promise<void>;
  downloadJournalAudio: (request: Request, response: Response) => Promise<void>;
}

const AUDIO_DIR = path.join(process.cwd(), "data", "audio");
const TEMP_DIR = path.join(process.cwd(), "data", "temp");

interface JournalControllerDeps {
  openAIService?: OpenAIChatService;
  historyStore?: ChatHistoryStore;
}

interface AssistantTurn {
  MessageId?: string;
  CharacterName?: string;
  Text?: string;
  Pinyin?: string;
  Tone?: string;
  Translation?: string;
}

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
  const journalReviewRepository = dataSource.getRepository(JournalReviewEntity);
  const messageRepository = dataSource.getRepository(MessageEntity);
  const characterRepository = dataSource.getRepository(CharacterEntity);
  const storyRepository = dataSource.getRepository(StoryEntity);
  const userRepository = dataSource.getRepository(UserEntity);
  const apiKey = process.env.OPENAI_API_KEY ?? "";
  const model = process.env.OPENAI_MODEL ?? "gpt-4.1-mini";
  const systemPromptPath = process.env.OPENAI_SYSTEM_PROMPT_PATH;
  const openAIService =
    deps.openAIService ?? (apiKey ? createOpenAIChatService({ apiKey, model, systemPromptPath }) : null);
  const historyStore = deps.historyStore ?? createChatHistoryStore();

  let hasLoggedAssistantReplyParseFailure = false;
  let hasLoggedStoryProgressUpdateFailure = false;

  const getSessionId = (value: unknown) => (typeof value === "string" ? value.trim() : "");
  const normalizeName = (value: string) => value.trim().toLowerCase();

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

  const applyAssistantEdits = (history: ChatHistoryMessage[]) => {
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
          const nextTurn = { ...turn, Text: updatedText };

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

  const buildMessageEntities = (
    history: ChatHistoryMessage[],
    userId: number,
    journalId: number,
    voiceByCharacter: Map<string, { voiceModel: string; voiceName: string; pitch: number | null; speakingRate: number | null }>
  ): Array<Pick<MessageEntity, "content" | "characterName" | "translation" | "pinyin" | "tone" | "audio" | "userId" | "journalId">> => {
    const result: Array<Pick<MessageEntity, "content" | "characterName" | "translation" | "pinyin" | "tone" | "audio" | "userId" | "journalId">> = [];

    for (const message of history) {
      if (message.role === "user") {
        const content = message.content.trim();
        if (!content) {
          continue;
        }

        result.push({
          content,
          characterName: "User",
          translation: null,
          pinyin: null,
          tone: null,
          audio: null,
          userId,
          journalId
        });
        continue;
      }

      if (message.role !== "assistant") {
        continue;
      }

      const turns = parseAssistantReply(message.content);
      if (!turns.length) {
        const fallback = message.content.trim();
        if (!fallback) {
          continue;
        }

        result.push({
          content: fallback,
          characterName: "Mimi",
          translation: null,
          pinyin: null,
          tone: null,
          audio: null,
          userId,
          journalId
        });
        continue;
      }

      for (const turn of turns) {
        const content = typeof turn.Text === "string" ? turn.Text.trim() : "";
        if (!content) {
          continue;
        }

        const characterName = typeof turn.CharacterName === "string" ? turn.CharacterName.trim() : "Mimi";
        const translation = typeof turn.Translation === "string" ? turn.Translation.trim() : "";
        const pinyin = typeof turn.Pinyin === "string" ? turn.Pinyin.trim() : "";
        const tone = typeof turn.Tone === "string" ? turn.Tone.trim() : "";
        const voiceKey = normalizeName(characterName || "Mimi");
        const voiceSettings = voiceByCharacter.get(voiceKey);
        const audio = tone ? buildAudioId(content, tone, `${voiceSettings?.voiceModel ?? "openai"}:${voiceSettings?.voiceName ?? ""}`, voiceSettings?.pitch ?? undefined, voiceSettings?.speakingRate ?? undefined) : null;

        result.push({
          content,
          characterName: characterName || "Mimi",
          translation: translation || null,
          pinyin: pinyin || null,
          tone: tone || null,
          audio,
          userId,
          journalId
        });
      }
    }

    return result;
  };

  const listJournals: JournalController["listJournals"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const rawStoryId = request.query?.storyId;
    let storyId = rawStoryId === undefined ? null : parseStoryId(rawStoryId);

    if (rawStoryId !== undefined && !storyId) {
      response.status(400).json({ message: "Invalid story id" });
      return;
    }

    try {
      // Fallback to user's currentStoryId if not provided
      if (storyId === null) {
        const user = await userRepository.findOne({ where: { id: request.user.id } });
        storyId = user?.currentStoryId ?? null;
      }

      const journals = await journalRepository.find({
        where: {
          userId: request.user.id,
          ...(storyId ? { storyId } : {})
        },
        order: { createdAt: "DESC" }
      });

      response.json({
        journals: journals.map((journal) => ({
          id: journal.id,
          summary: journal.summary,
          createdAt: journal.createdAt.toISOString()
        }))
      });
    } catch (error) {
      console.error("Error in listJournals:", error);
      response.status(500).json({
        message: "Failed to load journals",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  const getJournal: JournalController["getJournal"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const journalId = Number(request.params?.id);

    if (!Number.isInteger(journalId)) {
      response.status(400).json({ message: "Invalid journal id" });
      return;
    }

    try {
      const journal = await journalRepository.findOne({
        where: { id: journalId, userId: request.user.id }
      });

      if (!journal) {
        response.status(404).json({ message: "Journal not found" });
        return;
      }

      const messages = await messageRepository.find({
        where: { journalId: journal.id, userId: request.user.id },
        order: { createdAt: "ASC" }
      });

      response.json({
        journal: {
          id: journal.id,
          summary: journal.summary,
          createdAt: journal.createdAt.toISOString()
        },
        messages: messages.map((message) => ({
          id: message.id,
          content: message.content,
          characterName: message.characterName,
          translation: message.translation,
          pinyin: message.pinyin,
          tone: message.tone,
          audio: message.audio,
          createdAt: message.createdAt.toISOString()
        }))
      });
    } catch (error) {
      console.error("Error in getJournal:", error);
      response.status(500).json({
        message: "Failed to load journal",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Searches messages by query text (for vocabulary memory linking).
   * Supports regex patterns for flexible matching.
   */
  const searchMessages: JournalController["searchMessages"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const query = String(request.query?.q ?? "").trim();
    const limit = Math.min(Number(request.query?.limit ?? 50), 100);

    if (!query) {
      response.status(400).json({ message: "Query parameter 'q' is required" });
      return;
    }

    try {
      // Get all journals for the user with their messages
      const journals = await journalRepository.find({
        where: { userId: request.user.id },
        order: { createdAt: "DESC" }
      });

      const results: Array<{
        messageId: string;
        journalId: number;
        journalDate: string;
        content: string;
        characterName: string;
        translation: string | null;
        pinyin: string | null;
        tone: string | null;
        audio: string | null;
      }> = [];

      // Search through all messages
      for (const journal of journals) {
        const messages = await messageRepository.find({
          where: { journalId: journal.id, userId: request.user.id },
          order: { createdAt: "ASC" }
        });

        for (const message of messages) {
          const queryLower = query.toLowerCase();
          // Allow searching by message id (used by memory linking)
          let matches = message.id.toLowerCase().includes(queryLower);
          try {
            const regex = new RegExp(query, "i");
            matches = matches || regex.test(message.content) || (message.translation ? regex.test(message.translation) : false);
          } catch {
            // Invalid regex, fallback to includes
            matches = matches || message.content.toLowerCase().includes(queryLower) ||
              (message.translation?.toLowerCase().includes(queryLower) ?? false);
          }

          if (matches) {
            results.push({
              messageId: message.id,
              journalId: journal.id,
              journalDate: journal.createdAt.toISOString(),
              content: message.content,
              characterName: message.characterName,
              translation: message.translation ?? null,
              pinyin: message.pinyin ?? null,
              tone: message.tone ?? null,
              audio: message.audio ?? null
            });

            if (results.length >= limit) {
              break;
            }
          }
        }

        if (results.length >= limit) {
          break;
        }
      }

      response.json({
        results,
        total: results.length,
        hasMore: results.length >= limit
      });
    } catch (error) {
      console.error("Error in searchMessages:", error);
      response.status(500).json({
        message: "Failed to search messages",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  const endConversation: JournalController["endConversation"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const sessionId = getSessionId(request.body?.sessionId);
    let storyId = parseStoryId(request.body?.storyId);

    if (!openAIService) {
      response.status(500).json({ message: "OpenAI API key is not configured" });
      return;
    }

    try {
      // Fallback to user's currentStoryId if not provided
      if (!storyId) {
        const user = await userRepository.findOne({ where: { id: request.user.id } });
        storyId = user?.currentStoryId ?? null;
      }

      let story: StoryEntity | null = null;

      if (storyId) {
        story = await storyRepository.findOne({
          where: { id: storyId, userId: request.user.id }
        });

        if (!story) {
          response.status(404).json({ message: "Story not found" });
          return;
        }
      }

      const history = await historyStore.load(request.user.id);
      const adjustedHistory = applyAssistantEdits(history);
      const hasConversation = adjustedHistory.some((message) => message.role === "user" || message.role === "assistant");

      if (!hasConversation) {
        response.status(400).json({ message: "No conversation history to summarize" });
        return;
      }

      const summaryInstruction = `
Please summarize the above conversation in Vietnamese, update the story description, and return it in JSON format as follows:
{
  "Summary": "Summary of the conversation here.", -- Only the summary text in Vietnamese.
  "UpdatedStoryDescription": "The story description has been updated here." -- Only the story description text in Vietnamese.
}
`.trim();

      const summaryReply = await openAIService.createReply(undefined, [
        ...adjustedHistory,
        { role: "developer", content: summaryInstruction }
      ]);
      console.log("Summary reply from OpenAI:", summaryReply.reply);
      const summaryStory = JSON.parse(summaryReply.reply) as { Summary: string; UpdatedStoryDescription: string };

      const journal = journalRepository.create({
        summary: summaryStory.Summary,
        userId: request.user.id,
        storyId: story?.id ?? null
      });

      const savedJournal = await journalRepository.save(journal);
      const characters = await characterRepository.find({ where: { userId: request.user.id } });
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

      const messageEntities = buildMessageEntities(adjustedHistory, request.user.id, savedJournal.id, voiceByCharacter);

      if (messageEntities.length) {
        await messageRepository.save(messageEntities.map((message) => messageRepository.create(message)));
      }

      if (story) {
        try {
          const updatedProgress = summaryStory.UpdatedStoryDescription;
          if (updatedProgress) {
            story.currentProgress = updatedProgress;
            await storyRepository.save(story);
          }
        } catch (error) {
          if (!hasLoggedStoryProgressUpdateFailure) {
            console.warn("Story progress update failed; continuing without updating story progress.", error);
            hasLoggedStoryProgressUpdateFailure = true;
          }
        }
      }

      await historyStore.clear(request.user.id);

      response.json({
        journalId: savedJournal.id,
        summary: savedJournal.summary
      });
    } catch (error) {
      console.error("Error in endConversation:", error);
      response.status(500).json({
        message: "Failed to finalize journal",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Accepts a pre-computed summary from a local AI client (e.g. Ollama on PC).
   * Creates journal, saves message entities, updates story, and clears history
   * without calling any server-side AI service.
   *
   * Body: { summary: string, updatedStoryDescription?: string }
   */
  const endConversationLocal: JournalController["endConversationLocal"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const summary = typeof request.body?.summary === "string" ? request.body.summary.trim() : "";
    const updatedStoryDescription = typeof request.body?.updatedStoryDescription === "string"
      ? request.body.updatedStoryDescription.trim()
      : "";

    if (!summary) {
      response.status(400).json({ message: "Summary is required" });
      return;
    }

    const sessionId = getSessionId(request.body?.sessionId);
    let storyId = parseStoryId(request.body?.storyId);

    try {
      // Fallback to user's currentStoryId if not provided
      if (!storyId) {
        const user = await userRepository.findOne({ where: { id: request.user.id } });
        storyId = user?.currentStoryId ?? null;
      }

      let story: StoryEntity | null = null;

      if (storyId) {
        story = await storyRepository.findOne({
          where: { id: storyId, userId: request.user.id }
        });
      }

      const history = await historyStore.load(request.user.id);
      const adjustedHistory = applyAssistantEdits(history);

      const journal = journalRepository.create({
        summary,
        userId: request.user.id,
        storyId: story?.id ?? null
      });

      const savedJournal = await journalRepository.save(journal);
      const characters = await characterRepository.find({ where: { userId: request.user.id } });
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

      const messageEntities = buildMessageEntities(adjustedHistory, request.user.id, savedJournal.id, voiceByCharacter);

      if (messageEntities.length) {
        await messageRepository.save(messageEntities.map((message) => messageRepository.create(message)));
      }

      if (story && updatedStoryDescription) {
        try {
          story.currentProgress = updatedStoryDescription;
          await storyRepository.save(story);
        } catch (error) {
          if (!hasLoggedStoryProgressUpdateFailure) {
            console.warn("Story progress update failed (local); continuing without updating story progress.", error);
            hasLoggedStoryProgressUpdateFailure = true;
          }
        }
      }

      await historyStore.clear(request.user.id);

      response.json({
        journalId: savedJournal.id,
        summary: savedJournal.summary
      });
    } catch (error) {
      console.error("Error in endConversationLocal:", error);
      response.status(500).json({
        message: "Failed to finalize journal (local)",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  // ────────────────────────────────────────────────────────────────────────
  // FSRS Journal Review handlers
  // ────────────────────────────────────────────────────────────────────────

  /**
   * Returns journals that are due for review (nextReviewDate <= now)
   * plus all journals that have no review row yet (new / unreviewed).
   */
  const getDueJournals: JournalController["getDueJournals"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const userId = request.user.id;

      // 1. All journals for this user
      const allJournals = await journalRepository.find({
        where: { userId },
        order: { createdAt: "DESC" }
      });

      // 2. All existing review rows for this user
      const existingReviews = await journalReviewRepository.find({
        where: { userId }
      });

      const reviewByJournalId = new Map<number, JournalReviewEntity>();
      for (const review of existingReviews) {
        reviewByJournalId.set(review.journalId, review);
      }

      const now = new Date();
      const dueJournals: Array<{
        id: number;
        summary: string;
        createdAt: string;
        review: ReturnType<typeof serialiseJournalReview> | null;
      }> = [];

      for (const journal of allJournals) {
        const review = reviewByJournalId.get(journal.id);

        if (!review) {
          // New / unreviewed journal — always include
          dueJournals.push({
            id: journal.id,
            summary: journal.summary,
            createdAt: journal.createdAt.toISOString(),
            review: null
          });
          continue;
        }

        // Already reviewed — include only if due
        const nextReview = review.nextReviewDate instanceof Date
          ? review.nextReviewDate
          : new Date(String(review.nextReviewDate));

        if (nextReview <= now) {
          dueJournals.push({
            id: journal.id,
            summary: journal.summary,
            createdAt: journal.createdAt.toISOString(),
            review: serialiseJournalReview(review)
          });
        }
      }

      response.json({ journals: dueJournals });
    } catch (error) {
      console.error("Error in getDueJournals:", error);
      response.status(500).json({
        message: "Failed to load due journals",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Submits an FSRS review rating for a journal.
   * Creates the review row on first rating.
   *
   * Body: { journalId: number, rating: 1|2|3|4 }
   */
  const submitJournalReview: JournalController["submitJournalReview"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const { journalId, rating } = request.body as {
      journalId?: number;
      rating?: number;
    };

    if (!journalId || !Number.isInteger(journalId) || journalId <= 0) {
      response.status(400).json({ message: "Valid journalId is required" });
      return;
    }

    if (!rating || rating < 1 || rating > 4) {
      response.status(400).json({ message: "Rating must be 1–4" });
      return;
    }

    try {
      const userId = request.user.id;

      // Verify journal belongs to user
      const journal = await journalRepository.findOne({
        where: { id: journalId, userId }
      });

      if (!journal) {
        response.status(404).json({ message: "Journal not found" });
        return;
      }

      // Find or initialise review state
      let reviewEntity = await journalReviewRepository.findOne({
        where: { journalId, userId }
      });

      const currentState: ReviewState = reviewEntity
        ? {
          stability: reviewEntity.stability,
          difficulty: reviewEntity.difficulty,
          lapses: reviewEntity.lapses,
          currentIntervalDays: reviewEntity.currentIntervalDays,
          nextReviewDate: reviewEntity.nextReviewDate instanceof Date
            ? reviewEntity.nextReviewDate.toISOString()
            : String(reviewEntity.nextReviewDate),
          lastReviewDate: reviewEntity.lastReviewDate
            ? reviewEntity.lastReviewDate instanceof Date
              ? reviewEntity.lastReviewDate.toISOString()
              : String(reviewEntity.lastReviewDate)
            : null,
          reviewHistory: (() => {
            try {
              return JSON.parse(reviewEntity.reviewHistoryJson || "[]") as ReviewHistoryEntry[];
            } catch {
              return [];
            }
          })()
        }
        : createInitialReviewState();

      const updated = updateReviewAfterRating(currentState, rating as FSRSRating);

      const nextReview = {
        journalId,
        userId,
        stability: updated.stability,
        difficulty: updated.difficulty,
        lapses: updated.lapses,
        currentIntervalDays: updated.currentIntervalDays,
        nextReviewDate: new Date(updated.nextReviewDate),
        lastReviewDate: updated.lastReviewDate ? new Date(updated.lastReviewDate) : null,
        reviewHistoryJson: JSON.stringify(updated.reviewHistory)
      };

      const saved = await journalReviewRepository.save(
        reviewEntity
          ? { ...reviewEntity, ...nextReview }
          : journalReviewRepository.create(nextReview)
      );

      response.json({
        journal: {
          id: journal.id,
          summary: journal.summary,
          createdAt: journal.createdAt.toISOString()
        },
        review: serialiseJournalReview(saved)
      });
    } catch (error) {
      console.error("Error in submitJournalReview:", error);
      response.status(500).json({
        message: "Failed to submit journal review",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Serialises a journal review entity to a client-facing JSON shape.
   */
  const serialiseJournalReview = (entity: JournalReviewEntity) => {
    let reviewHistory: ReviewHistoryEntry[] = [];
    try {
      reviewHistory = JSON.parse(entity.reviewHistoryJson || "[]") as ReviewHistoryEntry[];
    } catch {
      reviewHistory = [];
    }

    return {
      id: entity.id,
      journalId: entity.journalId,
      stability: entity.stability,
      difficulty: entity.difficulty,
      lapses: entity.lapses,
      currentIntervalDays: entity.currentIntervalDays,
      nextReviewDate: entity.nextReviewDate,
      lastReviewDate: entity.lastReviewDate,
      reviewHistory
    };
  };

  // ────────────────────────────────────────────────────────────────────────
  // Download combined journal audio
  // ────────────────────────────────────────────────────────────────────────

  /**
   * Downloads all audio for a journal as a single concatenated MP3 file.
   * Messages are sorted by createdAt. Missing audio files are generated on the fly.
   *
   * GET /api/journals/:id/audio
   */
  const downloadJournalAudio: JournalController["downloadJournalAudio"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const journalId = Number(request.params?.id);
    if (!Number.isInteger(journalId) || journalId <= 0) {
      response.status(400).json({ message: "Invalid journal id" });
      return;
    }

    try {
      const userId = request.user.id;

      const journal = await journalRepository.findOne({
        where: { id: journalId, userId }
      });

      if (!journal) {
        response.status(404).json({ message: "Journal not found" });
        return;
      }

      const messages = await messageRepository.find({
        where: { journalId: journal.id, userId },
        order: { createdAt: "ASC" }
      });

      if (!messages.length) {
        response.status(404).json({ message: "No messages found for this journal" });
        return;
      }

      // Collect audio file paths for messages that have audio
      const characters = await characterRepository.find({ where: { userId } });
      const characterByName = new Map(
        characters.map((c) => [normalizeName(c.name), c])
      );

      const audioPaths: string[] = [];

      const resolveCharacterVoiceSettings = async (characterName: string) => {
        if (!characterName) {
          return {
            voiceModel: "openai" as string,
            voiceName: undefined as string | undefined,
            pitch: undefined as number | undefined,
            speakingRate: undefined as number | undefined
          };
        }

        if (characterName.toLowerCase() === "user") {
          const user = await userRepository
            .createQueryBuilder("user")
            .where("user.id = :userId", { userId })
            .getOne();

          return {
            voiceModel: "openai" as string,
            voiceName: user?.voiceName?.trim() || undefined,
            pitch: user?.pitch ?? undefined,
            speakingRate: undefined as number | undefined
          };
        }

        const character = await characterRepository
          .createQueryBuilder("character")
          .where("character.userId = :userId", { userId })
          .andWhere("LOWER(character.name) = LOWER(:name)", { name: characterName })
          .getOne();

        return {
          voiceModel: character?.voiceModel ?? "openai",
          voiceName: character?.voiceName?.trim() || undefined,
          pitch: character?.pitch ?? undefined,
          speakingRate: character?.speakingRate ?? undefined
        };
      };

      for (const message of messages) {
        let audioId: string | null = null;

        if (message.audio) {
          audioId = message.audio.trim();
        }
        else {
          const { voiceModel, voiceName, pitch, speakingRate } = await resolveCharacterVoiceSettings(message.characterName);
          const isGemini = voiceModel === "gemini";

          audioId = buildAudioId(
            message.content,
            message.tone ?? "neutral",
            `${voiceModel}:${voiceName ?? ""}`,
            pitch,
            speakingRate
          );
          message.audio = audioId;
          await messageRepository.save(message);
          try {
            await fs.access(getAudioPath(audioId));
          } catch {
            if (isGemini && voiceName) {
              await createGeminiTtsAudio(
                message.content,
                message.tone ?? "neutral",
                audioId,
                voiceName,
                pitch,
                speakingRate
              );
            } else {
              await createTtsAudio(
                message.content,
                message.tone ?? "neutral",
                audioId,
                voiceName,
                pitch,
                speakingRate
              );
            }
          }
        }
        const audioPath = getAudioPath(audioId);

        // Generate audio if it doesn't exist
        audioPaths.push(audioPath);
      }

      if (!audioPaths.length) {
        response.status(404).json({ message: "No audio available for this journal" });
        return;
      }

      // Concatenate all audio files using ffmpeg concat demuxer
      await fs.mkdir(TEMP_DIR, { recursive: true });
      const tempId = crypto.randomUUID();
      const concatListPath = path.join(TEMP_DIR, `concat_${tempId}.txt`);
      const outputPath = path.join(TEMP_DIR, `journal_${tempId}.mp3`);
      const silencePath = path.join(TEMP_DIR, `silence_${tempId}.mp3`);

      // Generate a 1-second silent MP3 to insert between messages
      await new Promise<void>((resolve, reject) => {
        execFile(
          ffmpegInstaller.path,
          [
            "-y",
            "-f", "lavfi",
            "-i", "anullsrc=r=44100:cl=mono",
            "-t", "1",
            "-codec:a", "libmp3lame",
            "-q:a", "2",
            silencePath
          ],
          (error) => {
            if (error) {
              reject(new Error(`ffmpeg silence generation failed: ${error.message}`));
              return;
            }
            resolve();
          }
        );
      });

      // Build the concat list file with 1s silence between each message
      const silenceLine = `file '${silencePath.replace(/\\/g, "/").replace(/'/g, "'\\''")}'`;
      const concatLines: string[] = [];
      for (let i = 0; i < audioPaths.length; i++) {
        if (i > 0) {
          concatLines.push(silenceLine);
        }
        concatLines.push(`file '${audioPaths[i].replace(/\\/g, "/").replace(/'/g, "'\\''")}' `);
      }
      const concatContent = concatLines.join("\n");
      await fs.writeFile(concatListPath, concatContent, "utf-8");

      await new Promise<void>((resolve, reject) => {
        execFile(
          ffmpegInstaller.path,
          [
            "-y",
            "-f", "concat",
            "-safe", "0",
            "-i", concatListPath,
            "-codec:a", "libmp3lame",
            "-q:a", "2",
            outputPath
          ],
          (error) => {
            if (error) {
              reject(new Error(`ffmpeg concat failed: ${error.message}`));
              return;
            }
            resolve();
          }
        );
      });

      // Send the file as a download
      const fileName = `journal_${journalId}.mp3`;
      response.setHeader("Content-Type", "audio/mpeg");
      response.setHeader("Content-Disposition", `attachment; filename="${fileName}"`);

      const fileBuffer = await fs.readFile(outputPath);
      response.send(fileBuffer);

      // Cleanup temp files
      await fs.unlink(concatListPath).catch(() => { });
      await fs.unlink(outputPath).catch(() => { });
      await fs.unlink(silencePath).catch(() => { });
    } catch (error) {
      console.error("Error in downloadJournalAudio:", error);
      response.status(500).json({
        message: "Failed to download journal audio",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return {
    listJournals,
    getJournal,
    searchMessages,
    endConversation,
    endConversationLocal,
    getDueJournals,
    submitJournalReview,
    downloadJournalAudio
  };
};
