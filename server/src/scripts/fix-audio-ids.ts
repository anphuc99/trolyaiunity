/**
 * Migration Script: Fix audio IDs in messages and my_log_messages tables
 *
 * The old code computed audio IDs without pitch and speakingRate parameters.
 * This script recalculates all audio IDs to match the new buildAudioId format.
 *
 * Usage:
 *   npx ts-node src/scripts/fix-audio-ids.ts
 *
 * Or add to package.json scripts:
 *   "fix:audio": "ts-node src/scripts/fix-audio-ids.ts"
 *   npm run fix:audio
 */

import crypto from "crypto";
import { AppDataSource } from "../data-source.js";
import MessageEntity from "../models/message.entity.js";
import MyLogMessageEntity from "../models/my-log-message.entity.js";
import CharacterEntity from "../models/character.entity.js";
import fs from "fs/promises";
import path from "path";
import { get } from "http";

const DEFAULT_SPEAKING_RATE = 1;
const DEFAULT_PITCH = 0;
const AUDIO_DIR = path.join(process.cwd(), "data", "audio");

/**
 * Normalizes text for stable hash generation (same as tts.service.ts).
 */
const normalizeForHash = (value: string) => {
  if (!value) {
    return "";
  }
  return value.replace(/[^a-zA-Z0-9\u3131-\uD79D]/g, "").toLowerCase();
};

/**
 * Builds the deterministic audio id (same as tts.service.ts).
 */
const buildAudioId = (
  text: string,
  tone: string,
  voice?: string,
  pitch?: number,
  speakingRate?: number
) => {
  return crypto
    .createHash("md5")
    .update(
      normalizeForHash(text) +
        normalizeForHash(tone) +
        normalizeForHash(voice ?? "") +
        normalizeForHash(`${pitch ?? DEFAULT_PITCH}`) +
        normalizeForHash(`${speakingRate ?? DEFAULT_SPEAKING_RATE}`)
    )
    .digest("hex");
};

const normalizeName = (value: string) => value.trim().toLowerCase();

const getAudioPath = (audioId: string) => path.join(AUDIO_DIR, `${audioId}.mp3`);

async function main() {
  console.log("Connecting to database...");
  await AppDataSource.initialize();

  const messageRepository = AppDataSource.getRepository(MessageEntity);
  const myLogMessageRepository = AppDataSource.getRepository(MyLogMessageEntity);
  const characterRepository = AppDataSource.getRepository(CharacterEntity);

  // Load all characters grouped by userId
  const allCharacters = await characterRepository.find();
  const charactersByUser = new Map<number, Map<string, CharacterEntity>>();

  for (const char of allCharacters) {
    if (!charactersByUser.has(char.userId)) {
      charactersByUser.set(char.userId, new Map());
    }
    charactersByUser.get(char.userId)!.set(normalizeName(char.name), char);
  }

  console.log(`Loaded ${allCharacters.length} characters.`);

  // Fix messages table
  console.log("\n=== Fixing messages table ===");
  const messages = await messageRepository.find();
  let messagesUpdated = 0;

  for (const msg of messages) {
    if (!msg.audio || !msg.tone || msg.characterName === "User") {
      continue;
    }

    const userChars = charactersByUser.get(msg.userId);
    const character = userChars?.get(normalizeName(msg.characterName));

    const oldAudioId = msg.audio;

    const newAudioId = buildAudioId(
      msg.content,
      msg.tone,
      `${character?.voiceModel ?? "openai"}:${character?.voiceName ?? ""}`,
      character?.pitch ?? undefined,
      character?.speakingRate ?? undefined
    );

    try {
      await fs.rename(getAudioPath(oldAudioId), getAudioPath(newAudioId));
    } catch {
      console.warn(`[Warning] Audio file for new ID not found: ${newAudioId} (message ID: ${msg.id})`);
    }

    if (msg.audio !== newAudioId) {
      console.log(`[messages] ${msg.id}: ${msg.audio} -> ${newAudioId}`);
      msg.audio = newAudioId;
      await messageRepository.save(msg);
      messagesUpdated++;
    }
  }

  console.log(`Updated ${messagesUpdated}/${messages.length} messages.`);

  // Fix my_log_messages table
  console.log("\n=== Fixing my_log_messages table ===");
  const myLogMessages = await myLogMessageRepository.find();
  let myLogMessagesUpdated = 0;

  for (const msg of myLogMessages) {
    if (!msg.audio || !msg.tone || msg.characterName === "User") {
      continue;
    }

    const userChars = charactersByUser.get(msg.userId);
    const character = userChars?.get(normalizeName(msg.characterName));

    const newAudioId = buildAudioId(
      msg.content,
      msg.tone,
      character?.voiceName || undefined,
      character?.pitch ?? undefined,
      character?.speakingRate ?? undefined
    );

    if (msg.audio !== newAudioId) {
      console.log(`[my_log_messages] ${msg.id}: ${msg.audio} -> ${newAudioId}`);
      msg.audio = newAudioId;
      await myLogMessageRepository.save(msg);
      myLogMessagesUpdated++;
    }
  }

  console.log(`Updated ${myLogMessagesUpdated}/${myLogMessages.length} my_log_messages.`);

  console.log("\n=== Migration complete ===");
  console.log(`Total: ${messagesUpdated + myLogMessagesUpdated} records updated.`);

  await AppDataSource.destroy();
}

main().catch((error) => {
  console.error("Migration failed:", error);
  process.exit(1);
});