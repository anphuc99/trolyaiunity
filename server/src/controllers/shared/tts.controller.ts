import type { Request, Response } from "express";
import fs from "fs/promises";
import type { DataSource } from "typeorm";
import CharacterEntity from "../../models/character.entity.js";
import { buildAudioId, createTtsAudio, getAudioPath } from "../../services/tts.service.js";

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

  const resolveCharacterVoiceSettings = async (userId: number, characterName: string) => {
    if (!characterName) {
      return {
        voiceName: undefined,
        pitch: undefined,
        speakingRate: undefined
      };
    }

    const character = await characterRepository
      .createQueryBuilder("character")
      .where("character.userId = :userId", { userId })
      .andWhere("LOWER(character.name) = LOWER(:name)", { name: characterName })
      .getOne();

    return {
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
    const audioId = buildAudioId(
      text,
      tone,
      resolvedSettings.voiceName,
      resolvedSettings.pitch,
      resolvedSettings.speakingRate
    );
    const audioPath = getAudioPath(audioId);

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
				response.json({ success: true, output: audioId, url: `/audio/${audioId}.wav` });
        return;
      } catch (error) {
        if ((error as NodeJS.ErrnoException)?.code !== "ENOENT") {
          console.error("Failed to access cached TTS audio.", error);
          throw error;
        }
      }

      await createTtsAudio(
        text,
        tone,
        audioId,
        resolvedSettings.voiceName,
        resolvedSettings.pitch,
        resolvedSettings.speakingRate
      );
      response.json({ success: true, output: audioId, url: `/audio/${audioId}.wav` });
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
