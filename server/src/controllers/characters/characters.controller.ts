import crypto from "crypto";
import { mkdir, writeFile } from "fs/promises";
import path from "path";
import type { Request, Response } from "express";
import type { DataSource } from "typeorm";
import CharacterEntity from "../../models/character.entity.js";

type CharacterGender = "male" | "female";

interface CharacterPayload {
  name: string;
  personality: string;
  gender: CharacterGender;
  appearance?: string | null;
  avatar?: string | null;
  voiceModel?: "openai" | null;
  voiceName?: string | null;
  pitch?: number | null;
  speakingRate?: number | null;
}

interface AvatarUploadPayload {
  image: string;
  filename?: string;
}

interface CharacterResponse extends CharacterPayload {
  id: number;
  createdAt: string;
  updatedAt: string;
}

interface CharactersController {
  listCharacters: (request: Request, response: Response) => Promise<void>;
  createCharacter: (request: Request, response: Response) => Promise<void>;
  updateCharacter: (request: Request, response: Response) => Promise<void>;
  deleteCharacter: (request: Request, response: Response) => Promise<void>;
  uploadAvatar: (request: Request, response: Response) => Promise<void>;
}

const MAX_AVATAR_BYTES = 2 * 1024 * 1024;
const AVATAR_DIR = path.join(process.cwd(), "public", "avatars");

const parseDataUrl = (dataUrl: string) => {
  const match = /^data:(image\/(png|jpeg|jpg|webp));base64,(.+)$/.exec(dataUrl);

  if (!match) {
    return null;
  }

  const mime = match[1];
  const buffer = Buffer.from(match[3], "base64");

  return {
    mime,
    buffer
  };
};

const normalizeVoiceName = (value: unknown) => {
  if (typeof value !== "string") {
    return null;
  }

  const trimmed = value.trim();
  return trimmed ? trimmed : null;
};

const resolveVoiceModel = (voiceModel: unknown, voiceName: string | null) => {
  if (!voiceModel) {
    return voiceName ? "openai" : null;
  }

  if (voiceModel !== "openai") {
    return "invalid" as const;
  }

  return "openai" as const;
};

const resolveExtension = (mime: string, filename?: string) => {
  if (mime === "image/png") {
    return "png";
  }

  if (mime === "image/webp") {
    return "webp";
  }

  const fallback = filename?.split(".").pop()?.toLowerCase();

  if (fallback === "jpg" || fallback === "jpeg") {
    return "jpg";
  }

  return "jpeg";
};

/**
 * Builds an absolute URL for a public asset using the request host.
 *
 * @param request - Express request with protocol/host context.
 * @param assetPath - Public path starting with "/".
 * @returns Absolute URL string for the asset.
 */
const buildAbsoluteUrl = (request: Request, assetPath: string) => {
  const host = request.get("host");

  if (!host) {
    return assetPath;
  }

  const normalizedPath = assetPath.startsWith("/") ? assetPath : `/${assetPath}`;

  return `${request.protocol}://${host}${normalizedPath}`;
};

const isValidGender = (value: unknown): value is CharacterGender => {
  return value === "male" || value === "female";
};

const toResponse = (entity: CharacterEntity): CharacterResponse => ({
  id: entity.id,
  name: entity.name,
  personality: entity.personality,
  gender: entity.gender,
  appearance: entity.appearance ?? null,
  avatar: entity.avatar ?? null,
  voiceModel: entity.voiceModel === "openai" ? "openai" : null,
  voiceName: entity.voiceName ?? null,
  pitch: entity.pitch ?? null,
  speakingRate: entity.speakingRate ?? null,
  createdAt: entity.createdAt.toISOString(),
  updatedAt: entity.updatedAt.toISOString()
});

const parseId = (value: string) => {
  const id = Number.parseInt(value, 10);
  return Number.isNaN(id) ? null : id;
};

/**
 * Builds the Characters controller with injected data source dependencies.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns The Characters controller handlers.
 */
export const createCharactersController = (dataSource: DataSource): CharactersController => {
  const repository = dataSource.getRepository(CharacterEntity);

  const listCharacters: CharactersController["listCharacters"] = async (_request, response) => {
    if (!_request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const characters = await repository.find({
        where: {
          userId: _request.user.id
        },
        order: {
          createdAt: "DESC"
        }
      });

      response.json(characters.map(toResponse));
    } catch (error) {
      console.error("Failed to load characters.", error);
      response.status(500).json({
        message: "Failed to load characters",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  const createCharacter: CharactersController["createCharacter"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const payload = request.body as CharacterPayload;
    const name = typeof payload?.name === "string" ? payload.name.trim() : "";
    const personality = typeof payload?.personality === "string" ? payload.personality.trim() : "";
    const gender = payload?.gender;
    const voiceName = normalizeVoiceName(payload?.voiceName);
    const voiceModel = resolveVoiceModel(payload?.voiceModel, voiceName);

    if (!name || !personality || !isValidGender(gender) || voiceModel === "invalid") {
      response.status(400).json({
        message: "Name, personality, and gender are required"
      });
      return;
    }

    try {
      const character = repository.create({
        name,
        personality,
        gender,
        appearance: payload?.appearance ?? null,
        avatar: payload?.avatar ?? null,
        voiceModel,
        voiceName,
        pitch: payload?.pitch ?? null,
        speakingRate: payload?.speakingRate ?? null,
        userId: request.user.id
      });

      const saved = await repository.save(character);

      response.status(201).json(toResponse(saved));
    } catch (error) {
      console.error("Failed to create character.", error);
      response.status(500).json({
        message: "Failed to create character",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  const updateCharacter: CharactersController["updateCharacter"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const id = parseId(request.params.id);

    if (!id) {
      response.status(400).json({
        message: "Invalid character id"
      });
      return;
    }

    const payload = request.body as CharacterPayload;
    const name = typeof payload?.name === "string" ? payload.name.trim() : "";
    const personality = typeof payload?.personality === "string" ? payload.personality.trim() : "";
    const gender = payload?.gender;
    const voiceName = normalizeVoiceName(payload?.voiceName);
    const voiceModel = resolveVoiceModel(payload?.voiceModel, voiceName);

    if (!name || !personality || !isValidGender(gender) || voiceModel === "invalid") {
      response.status(400).json({
        message: "Name, personality, and gender are required"
      });
      return;
    }

    try {
      const character = await repository.findOne({
        where: {
          id,
          userId: request.user.id
        }
      });

      if (!character) {
        response.status(404).json({
          message: "Character not found"
        });
        return;
      }

      character.name = name;
      character.personality = personality;
      character.gender = gender;
      character.appearance = payload?.appearance ?? null;
      character.avatar = payload?.avatar ?? null;
      character.voiceModel = voiceModel;
      character.voiceName = voiceName;
      character.pitch = payload?.pitch ?? null;
      character.speakingRate = payload?.speakingRate ?? null;

      const saved = await repository.save(character);

      response.json(toResponse(saved));
    } catch (error) {
      console.error("Failed to update character.", error);
      response.status(500).json({
        message: "Failed to update character",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  const deleteCharacter: CharactersController["deleteCharacter"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const id = parseId(request.params.id);

    if (!id) {
      response.status(400).json({
        message: "Invalid character id"
      });
      return;
    }

    try {
      const character = await repository.findOne({
        where: {
          id,
          userId: request.user.id
        }
      });

      if (!character) {
        response.status(404).json({
          message: "Character not found"
        });
        return;
      }

      await repository.remove(character);
      response.status(204).send();
    } catch (error) {
      console.error("Failed to delete character.", error);
      response.status(500).json({
        message: "Failed to delete character",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  const uploadAvatar: CharactersController["uploadAvatar"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const payload = request.body as AvatarUploadPayload;
    const image = typeof payload?.image === "string" ? payload.image.trim() : "";
    const filename = typeof payload?.filename === "string" ? payload.filename : undefined;

    if (!image) {
      response.status(400).json({
        message: "Image data is required"
      });
      return;
    }

    const parsed = parseDataUrl(image);

    if (!parsed) {
      response.status(400).json({
        message: "Unsupported image format"
      });
      return;
    }

    if (parsed.buffer.length > MAX_AVATAR_BYTES) {
      response.status(400).json({
        message: "Avatar image is too large"
      });
      return;
    }

    try {
      await mkdir(AVATAR_DIR, { recursive: true });
      const extension = resolveExtension(parsed.mime, filename);
      const avatarName = `${crypto.randomUUID()}.${extension}`;
      const avatarPath = path.join(AVATAR_DIR, avatarName);

      await writeFile(avatarPath, parsed.buffer);

      const publicPath = `/public/avatars/${avatarName}`;

      response.json({
        url: buildAbsoluteUrl(request, publicPath)
      });
    } catch (error) {
      console.error("Failed to upload avatar.", error);
      response.status(500).json({
        message: "Failed to upload avatar",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return {
    listCharacters,
    createCharacter,
    updateCharacter,
    deleteCharacter,
    uploadAvatar
  };
};
