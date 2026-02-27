import { execFile } from "child_process";
import crypto from "crypto";
import fs from "fs/promises";
import path from "path";
import { AudioContext, OfflineAudioContext } from "node-web-audio-api";
import * as WavEncoder from "wav-encoder";
import { createOpenAIClient } from "./openai.service.js";

const AUDIO_DIR = path.join(process.cwd(), "data", "audio");
const DEFAULT_MODEL = "gpt-4o-mini-tts-2025-03-20";
const DEFAULT_VOICE = "alloy";
const MAX_CHARS = 180;
const DEFAULT_SPEAKING_RATE = 1;
const DEFAULT_PITCH = 0;
const DETUNE_PER_PITCH_UNIT = 50;
const MIN_PLAYBACK_RATE = 0.1;
const MAX_PLAYBACK_RATE = 4;

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

const toArrayBuffer = (buffer: Buffer) =>
  buffer.buffer.slice(buffer.byteOffset, buffer.byteOffset + buffer.byteLength) as ArrayBuffer;

const clampPlaybackRate = (value: number) => Math.min(MAX_PLAYBACK_RATE, Math.max(MIN_PLAYBACK_RATE, value));

/**
 * Applies Web Audio API playbackRate + detune transform on a wav buffer.
 * Formula matches legacy client behavior: playbackRate = speakingRate, detune = pitch * 50.
 *
 * @param wavBuffer - Input wav buffer.
 * @param pitch - Pitch value used for detune calculation.
 * @param speakingRate - Playback speed multiplier.
 * @returns Transformed wav buffer.
 */
const applyWebAudioTransform = async (wavBuffer: Buffer, pitch?: number, speakingRate?: number) => {
  const resolvedPitch = Number.isFinite(pitch) ? (pitch as number) : DEFAULT_PITCH;
  const resolvedSpeakingRate = Number.isFinite(speakingRate)
    ? clampPlaybackRate(speakingRate as number)
    : DEFAULT_SPEAKING_RATE;
  const detuneCents = resolvedPitch * DETUNE_PER_PITCH_UNIT;
  const detuneFactor = Math.pow(2, detuneCents / 1200);
  const effectiveRate = clampPlaybackRate(resolvedSpeakingRate * detuneFactor);

  const decodeContext = new AudioContext();
  const decodedBuffer = await decodeContext.decodeAudioData(toArrayBuffer(wavBuffer));

  const renderLength = Math.max(1, Math.ceil(decodedBuffer.length / effectiveRate));
  const offlineContext = new OfflineAudioContext(
    decodedBuffer.numberOfChannels,
    renderLength,
    decodedBuffer.sampleRate
  );

  const source = offlineContext.createBufferSource();
  source.buffer = decodedBuffer;
  source.playbackRate.value = resolvedSpeakingRate;
  source.detune.value = detuneCents;
  source.connect(offlineContext.destination);
  source.start(0);

  const renderedBuffer = await offlineContext.startRendering();
  await decodeContext.close();

  const channelData: Float32Array[] = [];
  for (let channelIndex = 0; channelIndex < renderedBuffer.numberOfChannels; channelIndex++) {
    channelData.push(Float32Array.from(renderedBuffer.getChannelData(channelIndex)));
  }

  const encodedBuffer = await WavEncoder.encode({
    sampleRate: renderedBuffer.sampleRate,
    channelData
  });

  return Buffer.from(encodedBuffer);
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
 * Converts a WAV buffer to MP3 using ffmpeg.
 *
 * @param wavBuffer - Input WAV buffer.
 * @returns MP3 buffer.
 */
const convertWavToMp3 = async (wavBuffer: Buffer): Promise<Buffer> => {
  const tempId = crypto.randomUUID();
  const tempWavPath = path.join(AUDIO_DIR, `_tmp_${tempId}.wav`);
  const tempMp3Path = path.join(AUDIO_DIR, `_tmp_${tempId}.mp3`);

  try {
    await fs.writeFile(tempWavPath, wavBuffer);

    await new Promise<void>((resolve, reject) => {
      execFile(
        "ffmpeg",
        ["-y", "-i", tempWavPath, "-codec:a", "libmp3lame", "-q:a", "2", tempMp3Path],
        (error) => {
          if (error) {
            reject(new Error(`ffmpeg conversion failed: ${error.message}`));
          } else {
            resolve();
          }
        }
      );
    });

    return await fs.readFile(tempMp3Path);
  } finally {
    await fs.unlink(tempWavPath).catch(() => {});
    await fs.unlink(tempMp3Path).catch(() => {});
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
  const transformedWav = await applyWebAudioTransform(rawBuffer, pitch, speakingRate);
  const mp3Buffer = await convertWavToMp3(transformedWav);
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
