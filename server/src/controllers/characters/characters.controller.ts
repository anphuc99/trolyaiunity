import crypto from "crypto";
import { mkdir, writeFile } from "fs/promises";
import path from "path";
import type { Request, Response } from "express";
import type { DataSource } from "typeorm";
import CharacterEntity from "../../models/character.entity.js";

interface CharacterPayload {
  name: string;
  personality: string;
  gender: string;
  age?: number | null;
  appearance?: string | null;
  avatar?: string | null;
  voiceModel?: string | null;
  voiceName?: string | null;
  pitch?: number | null;
  speakingRate?: number | null;
}

interface AvatarUploadPayload {
  image: string;
  filename?: string;
}

interface CharacterResponse {
  id: number;
  name: string;
  personality: string;
  gender: string;
  age: number | null;
  appearance: string | null;
  avatar: string | null;
  voiceModel: string | null;
  voiceName: string | null;
  pitch: number | null;
  speakingRate: number | null;
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

/**
 * Parses a base64 data URL for an image and extracts mime type + binary buffer.
 */
const parseDataUrl = (dataUrl: string) => {
  const match = /^data:(image\/(png|jpeg|jpg|webp));base64,(.+)$/.exec(dataUrl);

  if (!match) {
    return null;
  }

  const mime = match[1];
  const buffer = Buffer.from(match[3], "base64");

  return { mime, buffer };
};

/**
 * Infers file extension from mime type with optional filename fallback.
 */
const resolveExtension = (mime: string, filename?: string) => {
  if (mime === "image/png") return "png";
  if (mime === "image/webp") return "webp";

  const fallback = filename?.split(".").pop()?.toLowerCase();
  if (fallback === "jpg" || fallback === "jpeg") return "jpg";

  return "jpeg";
};

/**
 * Builds an absolute URL for a public asset using the request host.
 */
const buildAbsoluteUrl = (request: Request, assetPath: string) => {
  const host = request.get("host");
  if (!host) return assetPath;

  const normalizedPath = assetPath.startsWith("/") ? assetPath : `/${assetPath}`;
  return `${request.protocol}://${host}${normalizedPath}`;
};

const VALID_GENDERS = new Set(["male", "female"]);

const toResponse = (entity: CharacterEntity): CharacterResponse => ({
  id: entity.id,
  name: entity.name,
  personality: entity.personality,
  gender: entity.gender,
  age: entity.age ?? null,
  appearance: entity.appearance ?? null,
  avatar: entity.avatar ?? null,
  voiceModel: entity.voiceModel ?? null,
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

const parseAge = (value: unknown) => {
  if (value == null || value === "") {
    return { value: null, valid: true as const };
  }

  const parsed = typeof value === "number" ? value : Number.parseInt(String(value), 10);

  if (!Number.isInteger(parsed) || parsed < 0 || parsed > 150) {
    return { value: null, valid: false as const };
  }

  return { value: parsed, valid: true as const };
};

/**
 * Builds the Characters controller with injected data source.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns The Characters controller handlers.
 */
export const createCharactersController = (dataSource: DataSource): CharactersController => {
  const repository = dataSource.getRepository(CharacterEntity);

  const listCharacters: CharactersController["listCharacters"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const characters = await repository.find({
        where: { userId: request.user.id },
        order: { createdAt: "DESC" }
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
    const gender = typeof payload?.gender === "string" ? payload.gender.trim().toLowerCase() : "";

    if (!name) {
      response.status(400).json({ message: "Character name is required" });
      return;
    }

    if (!personality) {
      response.status(400).json({ message: "Character personality is required" });
      return;
    }

    if (!VALID_GENDERS.has(gender)) {
      response.status(400).json({ message: "Gender must be 'male' or 'female'" });
      return;
    }

    const ageResult = parseAge(payload?.age);
    if (!ageResult.valid) {
      response.status(400).json({ message: "Invalid age value" });
      return;
    }

    const appearance = typeof payload?.appearance === "string" ? payload.appearance.trim() || null : null;
    const voiceModel = typeof payload?.voiceModel === "string" ? payload.voiceModel.trim() || null : null;
    const voiceName = typeof payload?.voiceName === "string" ? payload.voiceName.trim() || null : null;
    const pitch = typeof payload?.pitch === "number" ? payload.pitch : null;
    const speakingRate = typeof payload?.speakingRate === "number" ? payload.speakingRate : null;

    try {
      const character = repository.create({
        name,
        personality,
        gender: gender as "male" | "female",
        age: ageResult.value,
        appearance,
        avatar: typeof payload?.avatar === "string" ? payload.avatar.trim() || null : null,
        voiceModel,
        voiceName,
        pitch,
        speakingRate,
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
      response.status(400).json({ message: "Invalid character ID" });
      return;
    }

    const body = request.body as Record<string, unknown>;

    try {
      const character = await repository.findOne({
        where: { id, userId: request.user.id }
      });

      if (!character) {
        response.status(404).json({ message: "Character not found" });
        return;
      }

      if (typeof body.name === "string") {
        const name = body.name.trim();
        if (!name) {
          response.status(400).json({ message: "Character name cannot be empty" });
          return;
        }
        character.name = name;
      }

      if (typeof body.personality === "string") {
        const personality = body.personality.trim();
        if (!personality) {
          response.status(400).json({ message: "Character personality cannot be empty" });
          return;
        }
        character.personality = personality;
      }

      if (typeof body.gender === "string") {
        const gender = body.gender.trim().toLowerCase();
        if (!VALID_GENDERS.has(gender)) {
          response.status(400).json({ message: "Gender must be 'male' or 'female'" });
          return;
        }
        character.gender = gender as "male" | "female";
      }

      if (body.age !== undefined) {
        const ageResult = parseAge(body.age);
        if (!ageResult.valid) {
          response.status(400).json({ message: "Invalid age value" });
          return;
        }
        character.age = ageResult.value;
      }

      if (body.appearance !== undefined) {
        character.appearance = typeof body.appearance === "string" ? body.appearance.trim() || null : null;
      }

      if (body.voiceModel !== undefined) {
        character.voiceModel = typeof body.voiceModel === "string" ? body.voiceModel.trim() || null : null;
      }

      if (body.voiceName !== undefined) {
        character.voiceName = typeof body.voiceName === "string" ? body.voiceName.trim() || null : null;
      }

      if (body.pitch !== undefined) {
        character.pitch = typeof body.pitch === "number" ? body.pitch : null;
      }

      if (body.speakingRate !== undefined) {
        character.speakingRate = typeof body.speakingRate === "number" ? body.speakingRate : null;
      }

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
      response.status(400).json({ message: "Invalid character ID" });
      return;
    }

    try {
      const character = await repository.findOne({
        where: { id, userId: request.user.id }
      });

      if (!character) {
        response.status(404).json({ message: "Character not found" });
        return;
      }

      await repository.remove(character);
      response.json({ message: "Character deleted" });
    } catch (error) {
      console.error("Failed to delete character.", error);
      response.status(500).json({
        message: "Failed to delete character",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  /**
   * Uploads an avatar image for a character.
   * Accepts a base64 data URL (max 2 MB). Saves to public/avatars/.
   */
  const uploadAvatar: CharactersController["uploadAvatar"] = async (request, response) => {
    if (!request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    const body = request.body as AvatarUploadPayload;
    const image = typeof body?.image === "string" ? body.image : "";
    const filename = typeof body?.filename === "string" ? body.filename : undefined;

    if (!image) {
      response.status(400).json({ message: "Image data URL is required" });
      return;
    }

    const parsed = parseDataUrl(image);
    if (!parsed) {
      response.status(400).json({ message: "Invalid image data URL. Supported formats: png, jpeg, jpg, webp" });
      return;
    }

    if (parsed.buffer.length > MAX_AVATAR_BYTES) {
      response.status(400).json({ message: `Image exceeds ${MAX_AVATAR_BYTES / (1024 * 1024)} MB limit` });
      return;
    }

    try {
      const ext = resolveExtension(parsed.mime, filename);
      const hash = crypto.createHash("sha256").update(parsed.buffer).digest("hex").slice(0, 16);
      const avatarFilename = `${hash}.${ext}`;

      await mkdir(AVATAR_DIR, { recursive: true });
      await writeFile(path.join(AVATAR_DIR, avatarFilename), parsed.buffer);

      const avatarUrl = buildAbsoluteUrl(request, `/public/avatars/${avatarFilename}`);

      response.json({ url: avatarUrl });
    } catch (error) {
      console.error("Failed to upload avatar.", error);
      response.status(500).json({
        message: "Failed to upload avatar",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return { listCharacters, createCharacter, updateCharacter, deleteCharacter, uploadAvatar };
};
