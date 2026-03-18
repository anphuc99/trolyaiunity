import type { Request, Response } from "express";
import type { DataSource } from "typeorm";
import VoiceEntity from "../../models/voice.entity.js";

interface VoicesController {
  listVoices: (request: Request, response: Response) => Promise<void>;
}

/**
 * Builds the shared Voices controller.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns The Voices controller handlers.
 */
export const createVoicesController = (dataSource: DataSource): VoicesController => {
  const repository = dataSource.getRepository(VoiceEntity);

  const listVoices: VoicesController["listVoices"] = async (_request, response) => {
    try {
      const voices = await repository.find({
        order: {
          model: "ASC",
          voice: "ASC"
        }
      });

      response.json({
        voices: voices.map((entry) => ({
          id: entry.id,
          model: entry.model,
          voice: entry.voice
        }))
      });
    } catch (error) {
      console.error("Failed to load voices.", error);
      response.status(500).json({
        message: "Failed to load voices",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return { listVoices };
};