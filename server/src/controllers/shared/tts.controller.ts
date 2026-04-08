import type { Request, Response } from "express";
import fs from "fs/promises";
import type { DataSource } from "typeorm";
import CharacterEntity from "../../models/character.entity.js";
import MessageEntity from "../../models/message.entity.js";
import MyLogMessageEntity from "../../models/my-log-message.entity.js";
import UserEntity from "../../models/user.entity.js";
import { createChatHistoryStore } from "../../services/chat-history.service.js";
import { buildAudioId, createTtsAudio, createGeminiTtsAudio, getAudioPath } from "../../services/tts.service.js";

interface TtsController {
  getTextToSpeech: (request: Request, response: Response) => Promise<void>;
}

type AssistantTurnShape = {
  MessageId?: string;
  CharacterName?: string;
  Text?: string;
  Pinyin?: string;
  [key: string]: unknown;
};

const MAX_TTS_CONTEXT_TURNS = 5;

const normalizeRecentTurns = (turns: string[]): string[] => {
  return turns
    .map((turn) => turn.trim())
    .filter((turn) => turn.length > 0)
    .slice(-MAX_TTS_CONTEXT_TURNS);
};

const formatContextTurn = (characterName: string, content: string): string => {
  const safeCharacterName = characterName?.trim() || "Unknown";
  const safeContent = content?.trim() || "";
  return `${safeCharacterName}: ${safeContent}`;
};

const parseRecentTurnsFromQuery = (rawRecentTurns: unknown): string[] => {
  if (Array.isArray(rawRecentTurns)) {
    return normalizeRecentTurns(
      rawRecentTurns
        .map((item) => (typeof item === "string" ? item : ""))
        .filter((item) => item.length > 0)
    );
  }

  if (typeof rawRecentTurns !== "string") {
    return [];
  }

  const trimmed = rawRecentTurns.trim();
  if (!trimmed) {
    return [];
  }

  try {
    const parsed = JSON.parse(trimmed) as unknown;
    if (Array.isArray(parsed)) {
      return normalizeRecentTurns(parsed.map((item) => (typeof item === "string" ? item : "")));
    }
  } catch {
    // Fallback to newline-separated parsing.
  }

  return normalizeRecentTurns(trimmed.split(/\r?\n/));
};

const parseAssistantTurns = (content: string): AssistantTurnShape[] => {
  const trimmed = content.trim();
  if (!trimmed) {
    return [];
  }

  try {
    const parsed = JSON.parse(trimmed) as unknown;

    if (Array.isArray(parsed)) {
      return parsed as AssistantTurnShape[];
    }

    if (parsed && typeof parsed === "object") {
      return [parsed as AssistantTurnShape];
    }
  } catch {
    return [];
  }

  return [];
};

const buildAssistantRewriteNote = (messageId: string, text: string, pinyin: string): string => {
  const lines = [
    `Assistant message edited: ${messageId}.`,
    "New content:",
    text
  ];

  if (pinyin?.trim()) {
    lines.push("New pinyin:", pinyin.trim());
  }

  return lines.join("\n");
};

/**
 * Builds the shared TTS controller.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns The TTS controller handlers.
 */
export const createTtsController = (dataSource: DataSource): TtsController => {
  const characterRepository = dataSource.getRepository(CharacterEntity);
  const messageRepository = dataSource.getRepository(MessageEntity);
  const myLogMessageRepository = dataSource.getRepository(MyLogMessageEntity);
  const userRepository = dataSource.getRepository(UserEntity);
  const chatHistoryStore = createChatHistoryStore();

  const resolveRecentTurnsFromChatHistory = async (userId: number, messageId: string): Promise<string[]> => {
    const history = await chatHistoryStore.load(userId);

    for (let i = history.length - 1; i >= 0; i -= 1) {
      const message = history[i];
      if (message.role !== "assistant") {
        continue;
      }

      const turns = parseAssistantTurns(message.content);
      if (!turns.length) {
        continue;
      }

      const targetIndex = turns.findIndex((turn) => (turn.MessageId ?? "").trim() === messageId);
      if (targetIndex < 0) {
        continue;
      }

      const startIndex = Math.max(0, targetIndex - (MAX_TTS_CONTEXT_TURNS - 1));
      return normalizeRecentTurns(
        turns
          .slice(startIndex, targetIndex + 1)
          .map((turn) => {
            const text = typeof turn.Text === "string" ? turn.Text.trim() : "";
            if (!text) {
              return "";
            }

            const characterName = typeof turn.CharacterName === "string" ? turn.CharacterName.trim() : "";
            return formatContextTurn(characterName || "Mimi", text);
          })
          .filter((turn) => turn.length > 0)
      );
    }

    return [];
  };

  const resolveRecentTurnsByMessageId = async (userId: number, messageId: string): Promise<string[]> => {
    const trimmedMessageId = messageId.trim();
    if (!trimmedMessageId) {
      return [];
    }

    const message = await messageRepository.findOne({
      where: { id: trimmedMessageId, userId }
    });

    if (message) {
      const recentMessages = await messageRepository
        .createQueryBuilder("message")
        .select(["message.characterName", "message.content", "message.createdAt", "message.id"])
        .where("message.userId = :userId", { userId })
        .andWhere("message.journalId = :journalId", { journalId: message.journalId })
        .andWhere("message.createdAt <= :createdAt", { createdAt: message.createdAt })
        .orderBy("message.createdAt", "DESC")
        .addOrderBy("message.id", "DESC")
        .take(MAX_TTS_CONTEXT_TURNS)
        .getMany();

      return normalizeRecentTurns(
        recentMessages
          .reverse()
          .map((item) => formatContextTurn(item.characterName, item.content))
      );
    }

    const myLogMessage = await myLogMessageRepository.findOne({
      where: { id: trimmedMessageId, userId }
    });

    if (myLogMessage) {
      const recentMyLogMessages = await myLogMessageRepository
        .createQueryBuilder("message")
        .select(["message.characterName", "message.content", "message.createdAt", "message.id"])
        .where("message.userId = :userId", { userId })
        .andWhere("message.journalId = :journalId", { journalId: myLogMessage.journalId })
        .andWhere("message.createdAt <= :createdAt", { createdAt: myLogMessage.createdAt })
        .andWhere("message.isHidden = :isHidden", { isHidden: false })
        .orderBy("message.createdAt", "DESC")
        .addOrderBy("message.id", "DESC")
        .take(MAX_TTS_CONTEXT_TURNS)
        .getMany();

      return normalizeRecentTurns(
        recentMyLogMessages
          .reverse()
          .map((item) => formatContextTurn(item.characterName, item.content))
      );
    }

    return resolveRecentTurnsFromChatHistory(userId, trimmedMessageId);
  };

  const persistRewrittenContent = async (
    userId: number,
    messageId: string,
    rewrittenText: string,
    rewrittenPinyin: string,
    audioId: string
  ): Promise<void> => {
    if (!messageId) {
      return;
    }

    const message = await messageRepository.findOne({
      where: { id: messageId, userId }
    });

    if (message) {
      message.content = rewrittenText;
      if (rewrittenPinyin) {
        message.pinyin = rewrittenPinyin;
      }
      message.audio = audioId;
      await messageRepository.save(message);
      return;
    }

    const myLogMessage = await myLogMessageRepository.findOne({
      where: { id: messageId, userId }
    });

    if (myLogMessage) {
      myLogMessage.content = rewrittenText;
      myLogMessage.audio = audioId;
      await myLogMessageRepository.save(myLogMessage);
    }
  };

  const appendRewriteNoteToChatHistory = async (
    userId: number,
    messageId: string,
    rewrittenText: string,
    rewrittenPinyin: string
  ): Promise<void> => {
    if (!messageId || !rewrittenText) {
      return;
    }

    await chatHistoryStore.append(userId, [
      {
        role: "developer",
        content: buildAssistantRewriteNote(messageId, rewrittenText, rewrittenPinyin)
      }
    ]);
  };

  const resolveMessageContentByMessageId = async (
    userId: number,
    messageId: string
  ): Promise<{ text: string; pinyin: string } | null> => {
    if (!messageId) {
      return null;
    }

    const message = await messageRepository.findOne({
      where: { id: messageId, userId }
    });

    if (message) {
      return {
        text: message.content?.trim() || "",
        pinyin: message.pinyin?.trim() || ""
      };
    }

    const myLogMessage = await myLogMessageRepository.findOne({
      where: { id: messageId, userId }
    });

    if (myLogMessage) {
      return {
        text: myLogMessage.content?.trim() || "",
        pinyin: ""
      };
    }

    const history = await chatHistoryStore.load(userId);
    for (let i = history.length - 1; i >= 0; i -= 1) {
      const historyMessage = history[i];
      if (historyMessage.role !== "assistant") {
        continue;
      }

      const turns = parseAssistantTurns(historyMessage.content);
      for (const turn of turns) {
        if ((turn.MessageId ?? "").trim() !== messageId) {
          continue;
        }

        return {
          text: typeof turn.Text === "string" ? turn.Text.trim() : "",
          pinyin: typeof turn.Pinyin === "string" ? turn.Pinyin.trim() : ""
        };
      }
    }

    return null;
  };

  const resolveCharacterVoiceSettings = async (userId: number, characterName: string) => {
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

    const voiceModel = character?.voiceModel ?? "openai";

    return {
      voiceModel,
      voiceName: character?.voiceName?.trim() || undefined,
      pitch: character?.pitch ?? undefined,
      speakingRate: character?.speakingRate ?? undefined
    };
  };

  const getTextToSpeech: TtsController["getTextToSpeech"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const text = typeof request.query.text === "string" ? request.query.text.trim() : "";
    const tone = typeof request.query.tone === "string" ? request.query.tone.trim() : "neutral, medium pitch";
    const characterName =
      typeof request.query.characterName === "string" ? request.query.characterName.trim() : "";
    const messageId = typeof request.query.messageId === "string" ? request.query.messageId.trim() : "";
    const recentTurnsFromQuery = parseRecentTurnsFromQuery(request.query.recentTurns);
    const force = request.query.force === "true";

    if (!text) {
      response.status(400).json({ message: "Text is required" });
      return;
    }

    if (!characterName) {
      response.status(400).json({ message: "characterName is required" });
      return;
    }

    const userId = request.user.id;
    const recentTurns = recentTurnsFromQuery.length
      ? recentTurnsFromQuery
      : (messageId ? await resolveRecentTurnsByMessageId(userId, messageId) : []);

    const resolvedSettings = await resolveCharacterVoiceSettings(userId, characterName);
    const isGemini = resolvedSettings.voiceModel === "gemini";
    const audioId = buildAudioId(
      text,
      tone,
      `${resolvedSettings.voiceModel}:${resolvedSettings.voiceName ?? ""}`,
      resolvedSettings.pitch,
      resolvedSettings.speakingRate
    );
    const audioPath = getAudioPath(audioId);

    const attachAudioToMessageIfMissing = async (targetAudioId: string) => {
      if (!messageId) {
        return;
      }

      const message = await messageRepository.findOne({
        where: { id: messageId, userId }
      });

      if (message) {
        if (!message.audio || !message.audio.trim()) {
          message.audio = targetAudioId;
          await messageRepository.save(message);
        }

        return;
      }

      const myLogMessage = await myLogMessageRepository.findOne({
        where: { id: messageId, userId }
      });

      if (!myLogMessage) {
        return;
      }

      if (!myLogMessage.audio || !myLogMessage.audio.trim()) {
        myLogMessage.audio = targetAudioId;
        await myLogMessageRepository.save(myLogMessage);
      }
    };

    try {
      let effectiveText = text;
      let effectivePinyin = "";
      let wasRewritten = false;

      if (force) {
        await fs.unlink(audioPath).catch((error) => {
          if ((error as NodeJS.ErrnoException)?.code !== "ENOENT") {
            throw error;
          }
        });
      }

      try {
        await fs.access(audioPath);

        if (messageId) {
          try {
            const persisted = await resolveMessageContentByMessageId(userId, messageId);
            if (persisted?.text) {
              effectiveText = persisted.text;
            }
            effectivePinyin = persisted?.pinyin || "";
            wasRewritten = effectiveText !== text;
          } catch (persistedResolveError) {
            console.warn(`[TTS] failed to resolve persisted message content: ${String(persistedResolveError)}`);
          }
        }

        await attachAudioToMessageIfMissing(audioId);
        response.json({
          success: true,
          output: audioId,
          url: `/audio/${audioId}.mp3`,
          text: effectiveText,
          pinyin: effectivePinyin,
          rewritten: wasRewritten
        });
        return;
      } catch (error) {
        if ((error as NodeJS.ErrnoException)?.code !== "ENOENT") {
          console.error("Failed to access cached TTS audio.", error);
          throw error;
        }
      }

      if (isGemini && resolvedSettings.voiceName) {
        const geminiResult = await createGeminiTtsAudio(
          text,
          tone,
          audioId,
          resolvedSettings.voiceName,
          resolvedSettings.pitch,
          resolvedSettings.speakingRate,
          recentTurns
        );

        if (geminiResult.outputText) {
          effectiveText = geminiResult.outputText;
        }
        effectivePinyin = geminiResult.outputPinyin;
        wasRewritten = geminiResult.usedRewrittenText;

        if (wasRewritten && messageId) {
          await persistRewrittenContent(userId, messageId, effectiveText, effectivePinyin, audioId);
          await appendRewriteNoteToChatHistory(userId, messageId, effectiveText, effectivePinyin);
        }
      } else {
        await createTtsAudio(
          text,
          tone,
          audioId,
          resolvedSettings.voiceName,
          resolvedSettings.pitch,
          resolvedSettings.speakingRate
        );
      }
      await attachAudioToMessageIfMissing(audioId);
      response.json({
        success: true,
        output: audioId,
        url: `/audio/${audioId}.mp3`,
        text: effectiveText,
        pinyin: effectivePinyin,
        rewritten: wasRewritten
      });
    } catch (error) {
      console.error("Failed to generate TTS.", error);
      response.status(500).json({
        message: "Failed to generate TTS",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return { getTextToSpeech };
};
