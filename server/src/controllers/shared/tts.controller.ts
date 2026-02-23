import type { Request, Response } from "express";
import fs from "fs/promises";
import { buildAudioId, createTtsAudio, getAudioPath } from "../../services/tts.service.js";

interface TtsController {
  getTextToSpeech: (request: Request, response: Response) => Promise<void>;
}

/**
 * Builds the shared TTS controller.
 *
 * @returns The TTS controller handlers.
 */
export const createTtsController = (): TtsController => {
  const getTextToSpeech: TtsController["getTextToSpeech"] = async (request, response) => {
    const text = typeof request.query.text === "string" ? request.query.text.trim() : "";
    const tone = typeof request.query.tone === "string" ? request.query.tone.trim() : "neutral, medium pitch";
    const voice = typeof request.query.voice === "string" ? request.query.voice.trim() : "";
    const force = request.query.force === "true";

    if (!text) {
      response.status(400).json({ message: "Text is required" });
      return;
    }

    const audioId = buildAudioId(text, tone, voice || undefined);
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
        response.json({ success: true, output: audioId, url: `/audio/${audioId}.mp3` });
        return;
      } catch (error) {
        if ((error as NodeJS.ErrnoException)?.code !== "ENOENT") {
          console.error("Failed to access cached TTS audio.", error);
          throw error;
        }
      }

      await createTtsAudio(text, tone, audioId, voice || undefined);
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
