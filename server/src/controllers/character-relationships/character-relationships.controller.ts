import type { Request, Response } from "express";
import type { DataSource } from "typeorm";
import CharacterRelationshipEntity from "../../models/character-relationship.entity.js";
import CharacterEntity from "../../models/character.entity.js";
import { createRelationshipUpdateService, type RelationshipUpdateService } from "../../services/relationship-update.service.js";
import { createVectorMemoryService } from "../../services/vector-memory.service.js";
import { createCheapAIService } from "../../services/cheap-ai.service.js";
import { createChatHistoryStore } from "../../services/chat-history.service.js";
import UserEntity from "../../models/user.entity.js";
import StoryEntity from "../../models/story.entity.js";

// ────────────────────────────────────────────────────────────────────────────
// Types
// ────────────────────────────────────────────────────────────────────────────

type TargetType = "user" | "character";

interface RelationshipPayload {
  ownerCharacterId: number;
  targetType: TargetType;
  targetCharacterId?: number | null;
  relationshipKind: string;
  stableThought: string;
  temporaryThought?: string | null;
  stableEmotion?: number;
  currentEmotion?: number;
  emotionCause?: string | null;
}

interface RelationshipResponse {
  id: number;
  ownerCharacterId: number;
  targetType: TargetType;
  targetCharacterId: number | null;
  relationshipKind: string;
  stableThought: string;
  temporaryThought: string | null;
  stableEmotion: number;
  currentEmotion: number;
  emotionCause: string | null;
  createdAt: string;
  updatedAt: string;
}

interface CharacterRelationshipsController {
  listRelationships: (request: Request, response: Response) => Promise<void>;
  createRelationship: (request: Request, response: Response) => Promise<void>;
  updateRelationship: (request: Request, response: Response) => Promise<void>;
  deleteRelationship: (request: Request, response: Response) => Promise<void>;
  evaluateSession: (request: Request, response: Response) => Promise<void>;
}

// ────────────────────────────────────────────────────────────────────────────
// Helpers
// ────────────────────────────────────────────────────────────────────────────

const isValidTargetType = (value: unknown): value is TargetType =>
  value === "user" || value === "character";

/**
 * Clamps a numeric value to the emotion range [0, 10].
 *
 * @param value - Raw value from the request body.
 * @param fallback - Default when value is missing or non-numeric.
 * @returns Clamped float.
 */
const clampEmotion = (value: unknown, fallback: number): number => {
  if (typeof value !== "number" || !Number.isFinite(value)) return fallback;
  return Math.min(10, Math.max(0, value));
};

const parseId = (value: string): number | null => {
  const id = Number.parseInt(value, 10);
  return Number.isNaN(id) || id <= 0 ? null : id;
};

const toResponse = (entity: CharacterRelationshipEntity): RelationshipResponse => ({
  id: entity.id,
  ownerCharacterId: entity.ownerCharacterId,
  targetType: entity.targetType,
  targetCharacterId: entity.targetCharacterId ?? null,
  relationshipKind: entity.relationshipKind,
  stableThought: entity.stableThought,
  temporaryThought: entity.temporaryThought ?? null,
  stableEmotion: entity.stableEmotion,
  currentEmotion: entity.currentEmotion,
  emotionCause: entity.emotionCause ?? null,
  createdAt: entity.createdAt.toISOString(),
  updatedAt: entity.updatedAt.toISOString()
});

// ────────────────────────────────────────────────────────────────────────────
// Factory
// ────────────────────────────────────────────────────────────────────────────

/**
 * Builds the CharacterRelationships controller with injected data source.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns The controller handlers.
 */
export const createCharacterRelationshipsController = (
  dataSource: DataSource
): CharacterRelationshipsController => {
  const repository = dataSource.getRepository(CharacterRelationshipEntity);
  const characterRepository = dataSource.getRepository(CharacterEntity);

  /**
   * Lists all relationships owned by a specific character.
   * Query param: ?characterId=X
   */
  const listRelationships: CharacterRelationshipsController["listRelationships"] = async (
    request,
    response
  ) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const characterId = parseId(String(request.query.characterId ?? ""));

    if (!characterId) {
      response.status(400).json({ message: "characterId query parameter is required" });
      return;
    }

    try {
      const relationships = await repository.find({
        where: { ownerCharacterId: characterId, userId: request.user.id },
        order: { createdAt: "ASC" }
      });

      response.json(relationships.map(toResponse));
    } catch (error) {
      console.error("Failed to list relationships.", error);
      response.status(500).json({
        message: "Failed to list relationships",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Creates a new relationship record.
   */
  const createRelationship: CharacterRelationshipsController["createRelationship"] = async (
    request,
    response
  ) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const payload = request.body as RelationshipPayload;
    const ownerCharacterId =
      typeof payload?.ownerCharacterId === "number" ? payload.ownerCharacterId : 0;
    const targetType = payload?.targetType;
    const targetCharacterId =
      typeof payload?.targetCharacterId === "number" ? payload.targetCharacterId : null;
    const relationshipKind =
      typeof payload?.relationshipKind === "string" ? payload.relationshipKind.trim() : "";
    const stableThought =
      typeof payload?.stableThought === "string" ? payload.stableThought.trim() : "";

    if (!ownerCharacterId || !isValidTargetType(targetType) || !relationshipKind || !stableThought) {
      response.status(400).json({
        message:
          "ownerCharacterId, targetType, relationshipKind, and stableThought are required"
      });
      return;
    }

    if (targetType === "character" && !targetCharacterId) {
      response.status(400).json({
        message: "targetCharacterId is required when targetType is 'character'"
      });
      return;
    }

    try {
      // Verify owner character belongs to user
      const ownerExists = await characterRepository.findOne({
        where: { id: ownerCharacterId, userId: request.user.id }
      });

      if (!ownerExists) {
        response.status(404).json({ message: "Owner character not found" });
        return;
      }

      // Verify target character belongs to user (when applicable)
      if (targetType === "character" && targetCharacterId) {
        const targetExists = await characterRepository.findOne({
          where: { id: targetCharacterId, userId: request.user.id }
        });

        if (!targetExists) {
          response.status(404).json({ message: "Target character not found" });
          return;
        }
      }

      const entity = repository.create({
        ownerCharacterId,
        targetType,
        targetCharacterId: targetType === "user" ? null : targetCharacterId,
        relationshipKind,
        stableThought,
        temporaryThought:
          typeof payload?.temporaryThought === "string"
            ? payload.temporaryThought.trim() || null
            : null,
        stableEmotion: clampEmotion(payload?.stableEmotion, 5.0),
        currentEmotion: clampEmotion(payload?.currentEmotion, 5.0),
        emotionCause:
          typeof payload?.emotionCause === "string"
            ? payload.emotionCause.trim() || null
            : null,
        userId: request.user.id
      });

      const saved = await repository.save(entity);
      response.status(201).json(toResponse(saved));
    } catch (error) {
      console.error("Failed to create relationship.", error);
      response.status(500).json({
        message: "Failed to create relationship",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Updates an existing relationship record.
   */
  const updateRelationship: CharacterRelationshipsController["updateRelationship"] = async (
    request,
    response
  ) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const id = parseId(request.params.id);

    if (!id) {
      response.status(400).json({ message: "Invalid relationship id" });
      return;
    }

    const payload = request.body as Partial<RelationshipPayload>;

    try {
      const entity = await repository.findOne({
        where: { id, userId: request.user.id }
      });

      if (!entity) {
        response.status(404).json({ message: "Relationship not found" });
        return;
      }

      if (typeof payload.relationshipKind === "string" && payload.relationshipKind.trim()) {
        entity.relationshipKind = payload.relationshipKind.trim();
      }

      if (typeof payload.stableThought === "string" && payload.stableThought.trim()) {
        entity.stableThought = payload.stableThought.trim();
      }

      if (payload.temporaryThought !== undefined) {
        entity.temporaryThought =
          typeof payload.temporaryThought === "string"
            ? payload.temporaryThought.trim() || null
            : null;
      }

      if (typeof payload.stableEmotion === "number") {
        entity.stableEmotion = clampEmotion(payload.stableEmotion, entity.stableEmotion);
      }

      if (typeof payload.currentEmotion === "number") {
        entity.currentEmotion = clampEmotion(payload.currentEmotion, entity.currentEmotion);
      }

      if (payload.emotionCause !== undefined) {
        entity.emotionCause =
          typeof payload.emotionCause === "string"
            ? payload.emotionCause.trim() || null
            : null;
      }

      const saved = await repository.save(entity);
      response.json(toResponse(saved));
    } catch (error) {
      console.error("Failed to update relationship.", error);
      response.status(500).json({
        message: "Failed to update relationship",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Deletes a relationship record.
   */
  const deleteRelationship: CharacterRelationshipsController["deleteRelationship"] = async (
    request,
    response
  ) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const id = parseId(request.params.id);

    if (!id) {
      response.status(400).json({ message: "Invalid relationship id" });
      return;
    }

    try {
      const entity = await repository.findOne({
        where: { id, userId: request.user.id }
      });

      if (!entity) {
        response.status(404).json({ message: "Relationship not found" });
        return;
      }

      await repository.remove(entity);
      response.status(204).send();
    } catch (error) {
      console.error("Failed to delete relationship.", error);
      response.status(500).json({
        message: "Failed to delete relationship",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Triggers post-session relationship evaluation for active characters.
   * Runs asynchronously — returns 202 immediately while the pipeline runs.
   */
  const evaluateSession: CharacterRelationshipsController["evaluateSession"] = async (
    request,
    response
  ) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const body = request.body as { activeCharacterNames?: unknown };
    const activeCharacterNames = Array.isArray(body?.activeCharacterNames)
      ? (body.activeCharacterNames as unknown[])
          .filter((n): n is string => typeof n === "string" && n.trim().length > 0)
          .map((n) => n.trim())
      : [];

    if (activeCharacterNames.length === 0) {
      response.status(400).json({ message: "activeCharacterNames array is required" });
      return;
    }

    // Initialize the update service (only when ChromaDB is available)
    const chromaUrl = process.env.CHROMA_URL ?? "";
    if (!chromaUrl) {
      response.status(503).json({ message: "Long-term memory is not configured" });
      return;
    }

    // Resolve storyId from user's current story
    const userRepository = dataSource.getRepository(UserEntity);
    const storyRepository = dataSource.getRepository(StoryEntity);
    const user = await userRepository.findOne({ where: { id: request.user.id } });
    let storyId: number | null = null;
    if (user?.currentStoryId) {
      const story = await storyRepository.findOne({
        where: { id: user.currentStoryId, userId: request.user.id }
      });
      storyId = story?.id ?? null;
    }

    // Respond immediately (fire-and-forget pipeline)
    response.status(202).json({ message: "Evaluation started" });

    try {
      const vectorMemory = createVectorMemoryService({ chromaUrl });
      const cheapAI = createCheapAIService();
      const historyStore = createChatHistoryStore();
      const updateService = createRelationshipUpdateService({
        dataSource,
        cheapAI,
        vectorMemory,
        historyStore
      });

      await updateService.evaluateSession(request.user.id, activeCharacterNames, storyId);
      console.log("[EvaluateSession] Completed for:", activeCharacterNames.join(", "));
    } catch (error) {
      console.error("[EvaluateSession] Pipeline failed:", error);
    }
  };

  return { listRelationships, createRelationship, updateRelationship, deleteRelationship, evaluateSession };
};
