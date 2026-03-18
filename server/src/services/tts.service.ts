import { execFile } from "child_process";
import crypto from "crypto";
import fs from "fs/promises";
import ffmpegInstaller from "@ffmpeg-installer/ffmpeg";
import path from "path";
import { createOpenAIClient } from "./openai.service.js";
import { synthesizeGeminiTts } from "./gemini-tts.service.js";

const AUDIO_DIR = path.join(process.cwd(), "data", "audio");
const DEFAULT_MODEL = "gpt-4o-mini-tts-2025-03-20";
const DEFAULT_VOICE = "alloy";
const MAX_CHARS = 180;
const DEFAULT_SPEAKING_RATE = 1;
const DEFAULT_PITCH = 0;
const DETUNE_PER_PITCH_UNIT = 50;
const MIN_PLAYBACK_RATE = 0.1;
const MAX_PLAYBACK_RATE = 4;
const TRIM_SILENCE_THRESHOLD_DB = -65;
const TRIM_SILENCE_DURATION_SEC = 0.04;

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
export const buildAudioId = (text: string, tone: string, voice?: string, pitch?: number, speakingRate?: number) => {
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

const clampPlaybackRate = (value: number) => Math.min(MAX_PLAYBACK_RATE, Math.max(MIN_PLAYBACK_RATE, value));

/**
 * Reads the sample rate from a standard WAV buffer header (bytes 24-27, little-endian uint32).
 *
 * @param wavBuffer - WAV file buffer.
 * @returns Sample rate in Hz.
 */
const readWavSampleRate = (wavBuffer: Buffer): number => wavBuffer.readUInt32LE(24);

export const clampText = (text: string) => {
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
 * Converts a WAV buffer to MP3 using the bundled ffmpeg binary.
 * Optionally applies a varispeed transform (pitch + speed change) in a first pass.
 * Silence trimming runs in a second pass on the encoded MP3 so that encoder-delay
 * padding introduced by libmp3lame is also removed — matching the behavior of the
 * standalone trim-audio script.
 *
 * @param wavBuffer - Input WAV buffer.
 * @param pitch - Optional pitch adjustment (detune = pitch * 50 cents).
 * @param speakingRate - Optional playback speed multiplier.
 * @returns MP3 buffer.
 */
export const convertWavToMp3 = async (
  wavBuffer: Buffer,
  pitch?: number,
  speakingRate?: number
): Promise<Buffer> => {
  const resolvedPitch = Number.isFinite(pitch) ? (pitch as number) : DEFAULT_PITCH;
  const resolvedSpeakingRate = Number.isFinite(speakingRate)
    ? clampPlaybackRate(speakingRate as number)
    : DEFAULT_SPEAKING_RATE;
  const detuneCents = resolvedPitch * DETUNE_PER_PITCH_UNIT;
  const detuneFactor = Math.pow(2, detuneCents / 1200);
  const effectiveRate = clampPlaybackRate(resolvedSpeakingRate * detuneFactor);

  const tempId = crypto.randomUUID();
  const tempWavPath = path.join(AUDIO_DIR, `_tmp_${tempId}.wav`);
  const tempMp3Path = path.join(AUDIO_DIR, `_tmp_${tempId}.mp3`);
  const tempTrimmedPath = path.join(AUDIO_DIR, `_tmp_${tempId}_trimmed.mp3`);

  try {
    await fs.writeFile(tempWavPath, wavBuffer);

    // Pass 1: Convert WAV to MP3 with optional varispeed (pitch + speed).
    const pass1Args = ["-y", "-i", tempWavPath];

    if (effectiveRate !== 1) {
      const sampleRate = readWavSampleRate(wavBuffer);
      const scaledRate = Math.round(sampleRate * effectiveRate);
      pass1Args.push("-af", `asetrate=${scaledRate},aresample=${sampleRate}`);
    }

    pass1Args.push("-codec:a", "libmp3lame", "-q:a", "2", tempMp3Path);

    await new Promise<void>((resolve, reject) => {
      execFile(
        ffmpegInstaller.path,
        pass1Args,
        (error) => {
          if (error) {
            reject(new Error(`ffmpeg conversion failed: ${error.message}`));
            return;
          }

          resolve();
        }
      );
    });

    // Pass 2: Trim leading/trailing silence from the encoded MP3.
    // Running on the MP3 ensures encoder-delay padding is also removed.
    const trimFilter = [
      `silenceremove=start_periods=1:start_duration=${TRIM_SILENCE_DURATION_SEC}:start_threshold=${TRIM_SILENCE_THRESHOLD_DB}dB`,
      "areverse",
      `silenceremove=start_periods=1:start_duration=${TRIM_SILENCE_DURATION_SEC}:start_threshold=${TRIM_SILENCE_THRESHOLD_DB}dB`,
      "areverse"
    ].join(",");

    await new Promise<void>((resolve, reject) => {
      execFile(
        ffmpegInstaller.path,
        ["-y", "-i", tempMp3Path, "-af", trimFilter, tempTrimmedPath],
        (error) => {
          if (error) {
            reject(new Error(`ffmpeg silence trim failed: ${error.message}`));
            return;
          }

          resolve();
        }
      );
    });

    return await fs.readFile(tempTrimmedPath);
  } finally {
    await fs.unlink(tempWavPath).catch(() => {});
    await fs.unlink(tempMp3Path).catch(() => {});
    await fs.unlink(tempTrimmedPath).catch(() => {});
  }
};

/**
 * Creates a TTS audio file using OpenAI.
 * After applying pitch/speed transforms the result is converted to MP3 before saving.
 *
 * @param text - Text to synthesize.
 * @param tone - Tone instruction string.
 * @param audioId - Target audio file id (hash).
 * @param voice - Optional voice name override.
 * @param pitch - Optional pitch adjustment.
 * @param speakingRate - Optional playback speed multiplier.
 * @returns The audio file id.
 */
export const createTtsAudio = async (
  text: string,
  tone: string,
  audioId: string,
  voice?: string,
  pitch?: number,
  speakingRate?: number
) => {
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
    response_format: "wav",
    instructions: tone,
    speed: 0.8
  });

  const rawBuffer = Buffer.from(await response.arrayBuffer());
  const mp3Buffer = await convertWavToMp3(rawBuffer, pitch, speakingRate);
  const filePath = path.join(AUDIO_DIR, `${audioId}.mp3`);
  await fs.writeFile(filePath, mp3Buffer);

  return audioId;
};

/**
 * Creates a TTS audio file using the Gemini API.
 * The Gemini voice key is rotated automatically (round-robin).
 *
 * @param text - Text to synthesize.
 * @param tone - Tone/style instruction forwarded to Gemini as style prompt content.
 * @param audioId - Target audio file id (hash).
 * @param voiceName - Gemini prebuilt voice name.
 * @param pitch - Optional pitch adjustment.
 * @param speakingRate - Optional playback speed multiplier.
 * @returns The audio file id.
 */
export const createGeminiTtsAudio = async (
  text: string,
  tone: string,
  audioId: string,
  voiceName: string,
  pitch?: number,
  speakingRate?: number
) => {
  await fs.mkdir(AUDIO_DIR, { recursive: true });

  const wavBuffer = await synthesizeGeminiTts(clampText(text), voiceName, tone);
  const mp3Buffer = await convertWavToMp3(wavBuffer, pitch, speakingRate);
  const filePath = path.join(AUDIO_DIR, `${audioId}.mp3`);
  await fs.writeFile(filePath, mp3Buffer);

  return audioId;
};

/**
 * Resolves the audio file path for a given audio id.
 *
 * @param audioId - Audio hash id.
 * @returns The audio file path.
 */
export const getAudioPath = (audioId: string) => path.join(AUDIO_DIR, `${audioId}.mp3`);
