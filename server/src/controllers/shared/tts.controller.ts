import type { Request, Response } from "express";
import fs from "fs/promises";
import type { DataSource } from "typeorm";
import CharacterEntity from "../../models/character.entity.js";
import MessageEntity from "../../models/message.entity.js";
import MyLogMessageEntity from "../../models/my-log-message.entity.js";
import UserEntity from "../../models/user.entity.js";
import { createChatHistoryStore } from "../../services/chat-history.service.js";
import { createCheapAIService } from "../../services/cheap-ai.service.js";
import { buildAudioId, createTtsAudio, createGeminiTtsAudio, getAudioPath } from "../../services/tts.service.js";

interface TtsController {
  getTextToSpeech: (request: Request, response: Response) => Promise<void>;
}

const NO_AUDIO_ERROR_MARKER = "gemini tts returned no audio data";

/**
 * Checks whether a TTS error indicates all keys returned no audio.
 *
 * @param error - The caught error.
 * @returns True when all voice keys exhausted with no audio.
 */
const isNoAudioError = (error: unknown): boolean => {
  const message = error instanceof Error ? error.message : String(error ?? "");
  return message.toLowerCase().includes(NO_AUDIO_ERROR_MARKER);
};

/**
 * Builds a developer note for chat history when a message is rewritten.
 *
 * @param messageId - Original message id.
 * @param text - Rewritten text.
 * @param pinyin - Rewritten pinyin.
 * @returns Formatted developer note string.
 */
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

  /**
   * Resolves voice settings for a character.
   *
   * @param userId - Current user id.
   * @param characterName - Character name (or "user").
   * @returns Voice model, name, pitch, and speaking rate.
   */
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

  /**
   * Persists rewritten content/pinyin back to the message database record and
   * appends a developer note to the chat history file.
   *
   * @param userId - Current user id.
   * @param messageId - Target message id to update.
   * @param rewrittenText - New text content.
   * @param rewrittenPinyin - New pinyin (may be empty for non-Hanzi text).
   * @param newAudioId - Audio id for the rewritten audio file.
   */
  const persistRewrittenContent = async (
    userId: number,
    messageId: string,
    rewrittenText: string,
    rewrittenPinyin: string,
    newAudioId: string
  ): Promise<void> => {
    const message = await messageRepository.findOne({
      where: { id: messageId, userId }
    });

    if (message) {
      message.content = rewrittenText;
      if (rewrittenPinyin) {
        message.pinyin = rewrittenPinyin;
      }
      message.audio = newAudioId;
      await messageRepository.save(message);

      await chatHistoryStore.append(userId, [
        { role: "developer", content: buildAssistantRewriteNote(messageId, rewrittenText, rewrittenPinyin) }
      ]);
      return;
    }

    const myLogMessage = await myLogMessageRepository.findOne({
      where: { id: messageId, userId }
    });

    if (myLogMessage) {
      myLogMessage.content = rewrittenText;
      myLogMessage.audio = newAudioId;
      await myLogMessageRepository.save(myLogMessage);
    }
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

    const attachAudioToMessageIfMissing = async () => {
      if (!messageId) {
        return;
      }

      const message = await messageRepository.findOne({
        where: { id: messageId, userId }
      });

      if (message) {
        if (!message.audio || !message.audio.trim()) {
          message.audio = audioId;
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
        myLogMessage.audio = audioId;
        await myLogMessageRepository.save(myLogMessage);
      }
    };

    try {
      if (force) {
        await fs.unlink(audioPath).catch((error) => {
          if ((error as NodeJS.ErrnoException)?.code !== "ENOENT") {
            throw error;
          }
        });
      }

      try {
        await fs.access(audioPath);
        await attachAudioToMessageIfMissing();
        response.json({ success: true, output: audioId, url: `/audio/${audioId}.mp3` });
        return;
      } catch (error) {
        if ((error as NodeJS.ErrnoException)?.code !== "ENOENT") {
          console.error("Failed to access cached TTS audio.", error);
          throw error;
        }
      }

      if (isGemini && resolvedSettings.voiceName) {
        try {
          await createGeminiTtsAudio(
            text,
            tone,
            audioId,
            resolvedSettings.voiceName,
            resolvedSettings.pitch,
            resolvedSettings.speakingRate
          );
        } catch (geminiError) {
          // When all 4 keys return no-audio, rewrite the message and retry.
          if (isNoAudioError(geminiError) && messageId) {
            console.warn("[TTS] all voice keys exhausted with no audio; attempting semantic rewrite.");

            const cheapAI = createCheapAIService();
            const rewrittenText = await cheapAI.rewriteTextForTtsNoAudio(text, [], tone);

            if (rewrittenText && rewrittenText !== text) {
              const rewrittenPinyin = await cheapAI.transliterateChineseToPinyin(rewrittenText);

              const newAudioId = buildAudioId(
                rewrittenText,
                tone,
                `${resolvedSettings.voiceModel}:${resolvedSettings.voiceName ?? ""}`,
                resolvedSettings.pitch,
                resolvedSettings.speakingRate
              );

              await createGeminiTtsAudio(
                rewrittenText,
                tone,
                newAudioId,
                resolvedSettings.voiceName,
                resolvedSettings.pitch,
                resolvedSettings.speakingRate
              );

              await persistRewrittenContent(userId, messageId, rewrittenText, rewrittenPinyin, newAudioId);

              response.json({
                success: true,
                output: newAudioId,
                url: `/audio/${newAudioId}.mp3`,
                rewritten: true,
                text: rewrittenText,
                pinyin: rewrittenPinyin
              });
              return;
            }
          }

          throw geminiError;
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
      await attachAudioToMessageIfMissing();
      response.json({ success: true, output: audioId, url: `/audio/${audioId}.mp3` });
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
