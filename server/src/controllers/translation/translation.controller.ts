import type { Request, Response } from "express";
import type { DataSource, Repository } from "typeorm";
import { toFile, type Uploadable } from "openai/uploads";
import MessageEntity from "../../models/message.entity.js";
import JournalEntity from "../../models/journal.entity.js";
import TranslationCardEntity from "../../models/translation-card.entity.js";
import TranslationReviewEntity from "../../models/translation-review.entity.js";
import { createOpenAIClient } from "../../services/openai.service.js";
import {
  createInitialReviewState,
  updateReviewAfterRating,
  type FSRSRating,
  type ReviewHistoryEntry
} from "../../services/fsrs.service.js";

interface TranslationController {
  listCards: (request: Request, response: Response) => Promise<void>;
  getDueCards: (request: Request, response: Response) => Promise<void>;
  getStats: (request: Request, response: Response) => Promise<void>;
  getLearnCandidate: (request: Request, response: Response) => Promise<void>;
  getMessageContext: (request: Request, response: Response) => Promise<void>;
  reviewTranslation: (request: Request, response: Response) => Promise<void>;
  toggleStar: (request: Request, response: Response) => Promise<void>;
  explainTranslation: (request: Request, response: Response) => Promise<void>;
  transcribeAudio: (request: Request, response: Response) => Promise<void>;
}

interface TranslationControllerDeps {
  explainWithOpenAI?: (payload: {
    content: string;
    translation?: string | null;
    userTranslation?: string | null;
    characterName?: string;
  }) => Promise<string>;
  transcribeWithOpenAI?: (file: Uploadable) => Promise<string>;
}

const MAX_AUDIO_BYTES = 12 * 1024 * 1024;

/**
 * Serialises a translation review entity to a client-facing JSON shape.
 */
const serialiseReview = (entity: TranslationReviewEntity) => {
  let reviewHistory: ReviewHistoryEntry[] = [];

  try {
    reviewHistory = JSON.parse(entity.reviewHistoryJson || "[]") as ReviewHistoryEntry[];
  } catch {
    reviewHistory = [];
  }

  return {
    id: entity.id,
    translationCardId: entity.translationCardId,
    stability: entity.stability,
    difficulty: entity.difficulty,
    lapses: entity.lapses,
    currentIntervalDays: entity.currentIntervalDays,
    nextReviewDate: entity.nextReviewDate,
    lastReviewDate: entity.lastReviewDate,
    isStarred: entity.isStarred,
    reviewHistory
  };
};

/**
 * Serialises a translation card entity to a client-facing JSON shape.
 */
const serialiseCard = (entity: TranslationCardEntity) => {
  return {
    id: entity.id,
    messageId: entity.messageId,
    content: entity.content,
    translation: entity.translation,
    userTranslation: entity.userTranslation,
    characterName: entity.characterName,
    audio: entity.audio,
    explanationMd: entity.explanationMd,
    journalId: entity.journalId,
    userId: entity.userId,
    createdAt: entity.createdAt,
    updatedAt: entity.updatedAt
  };
};

/**
 * Parses a base64 audio data URL and extracts mime type and buffer.
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
 * Resolves the file extension from a mime type.
 */
const resolveAudioExtension = (mime: string) => {
  if (mime.includes("webm")) return "webm";
  if (mime.includes("wav")) return "wav";
  if (mime.includes("mpeg") || mime.includes("mp3")) return "mp3";
  if (mime.includes("ogg")) return "ogg";
  return "webm";
};

/**
 * Builds the prompt for grammar/vocabulary explanations.
 */
const buildExplanationPrompt = (payload: {
  content: string;
  translation?: string | null;
  userTranslation?: string | null;
  characterName?: string;
}) => {
  const translation = payload.translation?.trim() || "(Không có bản dich mẫu)";
  const userTranslation = payload.userTranslation?.trim();

return `Bạn là giáo sư tiếng Hàn. Hãy giải thích ngữ pháp và từ vựng trong câu sau, trả lời bằng Markdown (không dùng JSON).

Thông tin:
- Câu gốc (Korean): ${payload.content}
- Bản dịch mẫu (Việt): ${translation}
${userTranslation !== null? "- Bản dịch người học (Việt): ${userTranslation}": ""}

Yêu cầu trả lời:
1. Giải thích ngữ pháp chính (danh sách gạch đầu dòng).
2. Giải thích từ vựng quan trọng (danh sách gạch đầu dòng).
3. Nếu có lỗi thường gặp, nhắc nhanh 1-2 ý.
4. Ngắn gọn, dễ hiểu, không quá dài.`;
};
/**
 * Builds the translation controller.
 *
 * @param dataSource - Initialised TypeORM data source.
 * @returns Translation controller handlers.
 */
export const createTranslationController = (
  dataSource: DataSource,
  deps: TranslationControllerDeps = {}
): TranslationController => {
  const cardRepo: Repository<TranslationCardEntity> = dataSource.getRepository(TranslationCardEntity);
  const reviewRepo: Repository<TranslationReviewEntity> = dataSource.getRepository(TranslationReviewEntity);
  const messageRepo: Repository<MessageEntity> = dataSource.getRepository(MessageEntity);
  const journalRepo: Repository<JournalEntity> = dataSource.getRepository(JournalEntity);
  const toDateKey = (value: Date | string) =>
    new Date(value).toLocaleDateString("en-CA", { timeZone: "Asia/Ho_Chi_Minh" });
  const apiKey = process.env.OPENAI_API_KEY ?? "";
  const openAIClient = apiKey ? createOpenAIClient(apiKey) : null;
  const explainWithOpenAI =
    deps.explainWithOpenAI ??
    (async (payload) => {
      if (!openAIClient) {
        throw new Error("OpenAI API key is not configured");
      }

      const completion = await openAIClient.responses.create({
        model: "gpt-4o-mini",
        input: buildExplanationPrompt(payload)
      });

      const reply = completion.output_text?.trim() ?? "";

      if (!reply) {
        throw new Error("OpenAI returned an empty explanation");
      }

      return reply;
    });

  const transcribeWithOpenAI =
    deps.transcribeWithOpenAI ??
    (async (file) => {
      if (!openAIClient) {
        throw new Error("OpenAI API key is not configured");
      }

      const response = await openAIClient.audio.transcriptions.create({
        file,
        model: "gpt-4o-transcribe",
        language: "ko"
      });

      const transcript = response.text?.trim() ?? "";

      if (!transcript) {
        throw new Error("OpenAI returned an empty transcript");
      }

      return transcript;
    });

  /**
   * Lists all translation cards for the current user with journal info.
   */
  const listCards: TranslationController["listCards"] = async (request, response) => {
    const userId = request.user?.id;

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const cards = await cardRepo.find({
        where: { userId },
        order: { createdAt: "DESC" }
      });
      const reviews = await reviewRepo.find({ where: { userId } });
      const reviewMap = new Map<number, TranslationReviewEntity>(
        reviews.map((review: TranslationReviewEntity) => [review.translationCardId, review])
      );

      // Fetch journal summaries for the cards
      const journalIds = [...new Set(cards.map((card: TranslationCardEntity) => card.journalId))];
      const journals = journalIds.length
        ? await journalRepo.find({ where: journalIds.map((id) => ({ id })) })
        : [];
      const journalMap = new Map<number, string>(journals.map((j: JournalEntity) => [j.id, j.summary]));

      const items = cards.map((card: TranslationCardEntity) => {
        const review = reviewMap.get(card.id);
        return {
          ...serialiseCard(card),
          journalSummary: journalMap.get(card.journalId) ?? null,
          review: review ? serialiseReview(review) : null
        };
      });

      response.json({ cards: items });
    } catch (error) {
      console.error("Failed to list translation cards.", error);
      response.status(500).json({ message: "Failed to list translation cards" });
    }
  };

  /**
   * Returns translation cards that are due for review, sorted by createdAt ASC (oldest first) with journal info.
   */
  const getDueCards: TranslationController["getDueCards"] = async (request, response) => {
    const userId = request.user?.id;

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const reviews = await reviewRepo.find({ where: { userId } });
      const todayKey = toDateKey(new Date());
      const dueReviews = reviews.filter((review: TranslationReviewEntity) => toDateKey(review.nextReviewDate) <= todayKey);
      const cardIds = dueReviews.map((review: TranslationReviewEntity) => review.translationCardId);

      const cards = cardIds.length
        ? await cardRepo
            .createQueryBuilder("c")
            .innerJoin(MessageEntity, "m", "c.messageId = m.id")
            .where("c.id IN (:...ids)", { ids: cardIds })
            .andWhere("c.userId = :userId", { userId })
            .orderBy("m.createdAt", "ASC")
            .getMany()
        : [];

      // Fetch journal summaries for the cards
      const journalIds = [...new Set(cards.map((card: TranslationCardEntity) => card.journalId))];
      const journals = journalIds.length
        ? await journalRepo.find({ where: journalIds.map((id) => ({ id })) })
        : [];
      const journalMap = new Map<number, string>(journals.map((j: JournalEntity) => [j.id, j.summary]));

      const cardMap = new Map<number, TranslationCardEntity>(cards.map((card: TranslationCardEntity) => [card.id, card]));
      const reviewMap = new Map<number, TranslationReviewEntity>(dueReviews.map((review: TranslationReviewEntity) => [review.translationCardId, review]));

      // Use database sorted results directly (sorted by message.createdAt)
      const sortedCards = cards;
      const items = sortedCards.map((card: TranslationCardEntity) => {
        const review = reviewMap.get(card.id);
        return {
          ...serialiseCard(card),
          journalSummary: journalMap.get(card.journalId) ?? null,
          review: review ? serialiseReview(review) : null
        };
      });

      response.json({ cards: items, total: items.length });
    } catch (error) {
      console.error("Failed to get due translation cards.", error);
      response.status(500).json({ message: "Failed to get due translation cards" });
    }
  };

  /**
   * Returns aggregated translation drill stats.
   */
  const getStats: TranslationController["getStats"] = async (request, response) => {
    const userId = request.user?.id;

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const totalCards = await cardRepo.count({ where: { userId } });
      const totalReviews = await reviewRepo.count({ where: { userId } });
      const starredCount = await reviewRepo.count({ where: { userId, isStarred: true } });
      const allReviews = await reviewRepo.find({ where: { userId } });
      const now = new Date();
      const todayKey = toDateKey(now);
      const dueToday = allReviews.filter((review: TranslationReviewEntity) => toDateKey(review.nextReviewDate) <= todayKey).length;
      const withoutReview = totalCards - totalReviews;

      const todayStr = now.toLocaleDateString("en-CA", { timeZone: "Asia/Ho_Chi_Minh" });
      let difficultCount = 0;

      for (const review of allReviews) {
        let history: ReviewHistoryEntry[] = [];

        try {
          history = JSON.parse(review.reviewHistoryJson || "[]") as ReviewHistoryEntry[];
        } catch {
          continue;
        }

        const hasTodayDifficult = history.some((entry) => {
          const reviewDate = new Date(entry.date).toLocaleDateString("en-CA", { timeZone: "Asia/Ho_Chi_Minh" });
          return reviewDate === todayStr && (entry.rating === 1 || entry.rating === 2);
        });

        if (hasTodayDifficult) {
          difficultCount++;
        }
      }

      response.json({
        totalCards,
        withReview: totalReviews,
        withoutReview,
        dueToday,
        starredCount,
        difficultCount
      });
    } catch (error) {
      console.error("Failed to get translation stats.", error);
      response.status(500).json({ message: "Failed to get translation stats" });
    }
  };

  /**
   * Returns ALL messages that have not been turned into translation cards, sorted by createdAt ASC (oldest first) with journal info.
   */
  const getLearnCandidate: TranslationController["getLearnCandidate"] = async (request, response) => {
    const userId = request.user?.id;

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const existingCards = await cardRepo.find({ where: { userId } });
      const existingMessageIds = existingCards.map((card: TranslationCardEntity) => card.messageId);

      // Get the last 1000 message IDs for this user to limit the candidate pool
      const recentMessages = await messageRepo.find({
        where: { userId },
        order: { createdAt: "DESC" },
        take: 1000,
        select: ["id"]
      });

      const recentIds = recentMessages.map(m => m.id);

      if (recentIds.length === 0) {
        response.json({ candidates: [] });
        return;
      }

      const query = messageRepo
        .createQueryBuilder("m")
        .where("m.id IN (:...recentIds)", { recentIds })
        .andWhere("m.translation IS NOT NULL")
        .andWhere("m.translation != ''");

      if (existingMessageIds.length > 0) {
        query.andWhere("m.id NOT IN (:...ids)", { ids: existingMessageIds });
      }

      // Sort by createdAt ASC (oldest first) instead of random
      const messages = await query.orderBy("m.created_at", "ASC").getMany();

      if (!messages.length) {
        response.json({ candidates: [] });
        return;
      }

      // Fetch journal summaries
      const journalIds = [...new Set(messages.map((m: MessageEntity) => m.journalId))];
      const journals = journalIds.length
        ? await journalRepo.find({ where: journalIds.map((id) => ({ id })) })
        : [];
      const journalMap = new Map<number, string>(journals.map((j: JournalEntity) => [j.id, j.summary]));

      const candidates = messages.map((message: MessageEntity) => ({
        messageId: message.id,
        content: message.content,
        translation: message.translation,
        characterName: message.characterName,
        audio: message.audio ?? null,
        journalId: message.journalId,
        journalSummary: journalMap.get(message.journalId) ?? null,
        createdAt: message.createdAt
      }));

      response.json({ candidates });
    } catch (error) {
      console.error("Failed to fetch translation candidates.", error);
      response.status(500).json({ message: "Failed to fetch translation candidates" });
    }
  };

  /**
   * Returns 5 messages before and 5 messages after the given message in the same journal.
   * Content is returned as Vietnamese translation text for learning hints.
   */
  const getMessageContext: TranslationController["getMessageContext"] = async (request, response) => {
    const userId = request.user?.id;
    const messageId = String(request.params.messageId || "").trim();

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    if (!messageId) {
      response.status(400).json({ message: "messageId is required" });
      return;
    }

    try {
      const currentMessage = await messageRepo.findOne({ where: { id: messageId, userId } });

      if (!currentMessage) {
        response.status(404).json({ message: "Message not found" });
        return;
      }

      const journalMessages = await messageRepo.find({
        where: { userId, journalId: currentMessage.journalId },
        order: { createdAt: "ASC" }
      });

      const currentIndex = journalMessages.findIndex((item: MessageEntity) => item.id === messageId);

      if (currentIndex < 0) {
        response.json({ before: [], after: [] });
        return;
      }

      const toVietnameseText = (item: MessageEntity) => {
        const isUserMessage = item.characterName?.trim().toLowerCase() === "user";

        if (isUserMessage) {
          return item.content?.trim() || "(Tin nhan user rong)";
        }

        return item.translation?.trim() || "(Chua co ban dich tieng Viet)";
      };

      const before = journalMessages
        .slice(Math.max(0, currentIndex - 5), currentIndex)
        .map((item: MessageEntity) => ({
          messageId: item.id,
          characterName: item.characterName,
          text: toVietnameseText(item)
        }));

      const after = journalMessages
        .slice(currentIndex + 1, currentIndex + 6)
        .map((item: MessageEntity) => ({
          messageId: item.id,
          characterName: item.characterName,
          text: toVietnameseText(item)
        }));

      response.json({ before, after });
    } catch (error) {
      console.error("Failed to get translation context.", error);
      response.status(500).json({ message: "Failed to get translation context" });
    }
  };

  /**
   * Generates or returns cached AI explanation for a translation card.
   */
  const explainTranslation: TranslationController["explainTranslation"] = async (request, response) => {
    const userId = request.user?.id;

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const { cardId, messageId, userTranslation } = request.body as {
      cardId?: number;
      messageId?: string;
      userTranslation?: string;
    };

    if (!cardId && !messageId) {
      response.status(400).json({ message: "cardId or messageId is required" });
      return;
    }

    try {
      let card: TranslationCardEntity | null = null;

      if (cardId) {
        card = await cardRepo.findOne({ where: { id: cardId, userId } });
      } else if (messageId) {
        card = await cardRepo.findOne({ where: { messageId, userId } });
      }

      if (!card && messageId) {
        const message = await messageRepo.findOne({ where: { id: messageId, userId } });

        if (!message) {
          response.status(404).json({ message: "Message not found" });
          return;
        }

        card = cardRepo.create({
          messageId: message.id,
          content: message.content,
          translation: message.translation ?? null,
          userTranslation: userTranslation?.trim() || null,
          characterName: message.characterName,
          audio: message.audio ?? null,
          journalId: message.journalId,
          userId
        });

        card = await cardRepo.save(card);
      }

      if (!card) {
        response.status(404).json({ message: "Translation card not found" });
        return;
      }

      if (card.explanationMd && card.explanationMd.trim()) {
        response.json({ explanation: card.explanationMd, card: serialiseCard(card) });
        return;
      }

      if (userTranslation && userTranslation.trim()) {
        card.userTranslation = userTranslation.trim();
      }

      const explanation = await explainWithOpenAI({
        content: card.content,
        translation: card.translation,
        userTranslation: card.userTranslation,
        characterName: card.characterName
      });

      card.explanationMd = explanation;
      card = await cardRepo.save(card);

      response.json({ explanation: explanation, card: serialiseCard(card) });
    } catch (error) {
      console.error("Failed to explain translation card.", error);
      response.status(500).json({ message: "Failed to explain translation card" });
    }
  };

  /**
   * Submits a translation review rating and schedules the next review.
   */
  const reviewTranslation: TranslationController["reviewTranslation"] = async (request, response) => {
    const userId = request.user?.id;

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const { rating, messageId, cardId, userTranslation } = request.body as {
      rating?: number;
      messageId?: string;
      cardId?: number;
      userTranslation?: string;
    };

    if (!rating || rating < 1 || rating > 4) {
      response.status(400).json({ message: "Rating must be 1–4" });
      return;
    }

    try {
      let card: TranslationCardEntity | null = null;

      if (cardId) {
        card = await cardRepo.findOne({ where: { id: cardId, userId } });
      } else if (messageId) {
        card = await cardRepo.findOne({ where: { messageId, userId } });
      }

      if (!card) {
        if (!messageId) {
          response.status(400).json({ message: "messageId is required for new cards" });
          return;
        }

        const message = await messageRepo.findOne({ where: { id: messageId, userId } });

        if (!message) {
          response.status(404).json({ message: "Message not found" });
          return;
        }

        card = cardRepo.create({
          messageId: message.id,
          content: message.content,
          translation: message.translation ?? null,
          userTranslation: userTranslation?.trim() || null,
          characterName: message.characterName,
          audio: message.audio ?? null,
          journalId: message.journalId,
          userId
        });

        card = await cardRepo.save(card);
      } else if (userTranslation && userTranslation.trim()) {
        card.userTranslation = userTranslation.trim();
        card = await cardRepo.save(card);
      }

      const reviewEntity = await reviewRepo.findOne({
        where: { translationCardId: card.id, userId }
      });

      const currentState = reviewEntity
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
        translationCardId: card.id,
        userId,
        stability: updated.stability,
        difficulty: updated.difficulty,
        lapses: updated.lapses,
        currentIntervalDays: updated.currentIntervalDays,
        nextReviewDate: new Date(updated.nextReviewDate),
        lastReviewDate: updated.lastReviewDate ? new Date(updated.lastReviewDate) : null,
        reviewHistoryJson: JSON.stringify(updated.reviewHistory),
        isStarred: reviewEntity?.isStarred ?? false
      };

      const saved = await reviewRepo.save(reviewEntity ? { ...reviewEntity, ...nextReview } : reviewRepo.create(nextReview));

      response.json({
        card: serialiseCard(card),
        review: serialiseReview(saved)
      });
    } catch (error) {
      console.error("Failed to review translation card.", error);
      response.status(500).json({ message: "Failed to review translation card" });
    }
  };

  /**
   * Toggles the starred state for a translation card.
   */
  const toggleStar: TranslationController["toggleStar"] = async (request, response) => {
    const userId = request.user?.id;
    const cardId = Number(request.params.id);

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    if (!Number.isInteger(cardId)) {
      response.status(400).json({ message: "Invalid translation card ID" });
      return;
    }

    try {
      let reviewEntity = await reviewRepo.findOne({
        where: { translationCardId: cardId, userId }
      });

      if (!reviewEntity) {
        const state = createInitialReviewState();
        reviewEntity = reviewRepo.create({
          translationCardId: cardId,
          userId,
          stability: state.stability,
          difficulty: state.difficulty,
          lapses: state.lapses,
          currentIntervalDays: state.currentIntervalDays,
          nextReviewDate: new Date(state.nextReviewDate),
          lastReviewDate: null,
          reviewHistoryJson: JSON.stringify(state.reviewHistory),
          isStarred: true
        });
      } else {
        reviewEntity.isStarred = !reviewEntity.isStarred;
      }

      const saved = await reviewRepo.save(reviewEntity);
      response.json(serialiseReview(saved));
    } catch (error) {
      console.error("Failed to toggle translation star.", error);
      response.status(500).json({ message: "Failed to toggle translation star" });
    }
  };

  /**
   * Transcribes user audio using OpenAI for shadowing practice.
   */
  const transcribeAudio: TranslationController["transcribeAudio"] = async (request, response) => {
    const userId = request.user?.id;

    if (!userId) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const { audio } = request.body as { audio?: string };

    if (!audio || typeof audio !== "string") {
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
      const file = await toFile(parsed.buffer, `translation.${extension}`, { type: parsed.mime });
      const transcript = await transcribeWithOpenAI(file);
      response.json({ transcript });
    } catch (error) {
      console.error("Failed to transcribe audio.", error);
      response.status(500).json({ message: "Failed to transcribe audio" });
    }
  };

  return {
    listCards,
    getDueCards,
    getStats,
    getLearnCandidate,
    getMessageContext,
    reviewTranslation,
    toggleStar,
    explainTranslation,
    transcribeAudio
  };
};
