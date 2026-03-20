import type { Request, Response } from "express";
import fs from "fs/promises";
import type { DataSource } from "typeorm";
import CharacterEntity from "../../models/character.entity.js";
import MessageEntity from "../../models/message.entity.js";
import MyLogMessageEntity from "../../models/my-log-message.entity.js";
import UserEntity from "../../models/user.entity.js";
import { buildAudioId, createTtsAudio, createGeminiTtsAudio, getAudioPath } from "../../services/tts.service.js";

interface TtsController {
  getTextToSpeech: (request: Request, response: Response) => Promise<void>;
}

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
      resolvedSettings.voiceName,
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
        await createGeminiTtsAudio(
          text,
          tone,
          audioId,
          resolvedSettings.voiceName,
          resolvedSettings.pitch,
          resolvedSettings.speakingRate
        );
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
