import crypto from "crypto";
import fs from "fs/promises";
import path from "path";
import { createOpenAIClient } from "./openai.service.js";

const AUDIO_DIR = path.join(process.cwd(), "data", "audio");
const DEFAULT_MODEL = "gpt-4o-mini-tts-2025-03-20";
const DEFAULT_VOICE = "alloy";
const MAX_CHARS = 180;

/**
 * Normalizes text for stable hash generation.
 *
 * @param value - Input text or tone.
 * @returns Normalized string for hashing.
 */
export const normalizeForHash = (value: string) => {
  if (!value) {
    return "";
  }

  return value.replace(/[^a-zA-Z0-9\u3131-\uD79D]/g, "").toLowerCase();
};

/**
 * Builds the deterministic audio id for a text + tone pair.
 *
 * @param text - Spoken text content.
 * @param tone - Tone instruction used for TTS.
 * @param voice - Optional voice name for TTS.
 * @returns MD5 hash string for the audio file.
 */
export const buildAudioId = (text: string, tone: string, voice?: string) => {
  return crypto
    .createHash("md5")
    .update(normalizeForHash(text) + normalizeForHash(tone) + normalizeForHash(voice ?? ""))
    .digest("hex");
};

const clampText = (text: string) => {
  let finalText = text.trim();

  if (finalText.length > MAX_CHARS) {
    finalText = finalText.substring(0, MAX_CHARS);
    const lastBreak = Math.max(
      finalText.lastIndexOf("。"),
      finalText.lastIndexOf("."),
      finalText.lastIndexOf("!"),
      finalText.lastIndexOf("?"),
      finalText.lastIndexOf(","),
      finalText.lastIndexOf(" ")
    );
    if (lastBreak > MAX_CHARS * 0.5) {
      finalText = finalText.substring(0, lastBreak + 1);
    }
  }

  if (!/[.!?。！？]$/.test(finalText)) {
    finalText += ".";
  }

  return finalText;
};

/**
 * Creates a TTS audio file using OpenAI.
 *
 * @param text - Text to synthesize.
 * @param tone - Tone instruction string.
 * @param audioId - Target audio file id (hash).
 * @param voice - Optional voice name override.
 * @returns The audio file id.
 */
export const createTtsAudio = async (text: string, tone: string, audioId: string, voice?: string) => {
  const apiKey = process.env.OPENAI_API_KEY ?? "";
  if (!apiKey) {
    throw new Error("OpenAI API key is not configured");
  }

  const client = createOpenAIClient(apiKey);
  const model = process.env.OPENAI_TTS_MODEL ?? DEFAULT_MODEL;
  const resolvedVoice = voice?.trim() || (process.env.OPENAI_TTS_VOICE ?? DEFAULT_VOICE);

  await fs.mkdir(AUDIO_DIR, { recursive: true });

  const response = await client.audio.speech.create({
    model,
    input: clampText(text),
    voice: resolvedVoice,
    response_format: "mp3",
    instructions: tone,
    speed: 0.8
  });

  const buffer = Buffer.from(await response.arrayBuffer());
  const filePath = path.join(AUDIO_DIR, `${audioId}.mp3`);
  await fs.writeFile(filePath, buffer);

  return audioId;
};

/**
 * Resolves the audio file path for a given audio id.
 *
 * @param audioId - Audio hash id.
 * @returns The audio file path.
 */
export const getAudioPath = (audioId: string) => path.join(AUDIO_DIR, `${audioId}.mp3`);
