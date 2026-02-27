import crypto from "crypto";
import fs from "fs/promises";
import lamejs from "lamejs";
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
 * Converts a WAV buffer to MP3 using lamejs (pure JavaScript encoder).
 *
 * @param wavBuffer - Input WAV buffer.
 * @returns MP3 buffer.
 */
const convertWavToMp3 = async (wavBuffer: Buffer): Promise<Buffer> => {
  // Parse WAV header to extract format info
  const dataView = new DataView(wavBuffer.buffer, wavBuffer.byteOffset, wavBuffer.byteLength);

  // Skip to format chunk
  let offset = 12; // Skip RIFF header
  let numChannels = 1;
  let sampleRate = 44100;
  let bitsPerSample = 16;
  let dataOffset = 0;
  let dataSize = 0;

  while (offset < wavBuffer.length - 8) {
    const chunkId = String.fromCharCode(
      wavBuffer[offset],
      wavBuffer[offset + 1],
      wavBuffer[offset + 2],
      wavBuffer[offset + 3]
    );
    const chunkSize = dataView.getUint32(offset + 4, true);

    if (chunkId === "fmt ") {
      numChannels = dataView.getUint16(offset + 10, true);
      sampleRate = dataView.getUint32(offset + 12, true);
      bitsPerSample = dataView.getUint16(offset + 22, true);
    } else if (chunkId === "data") {
      dataOffset = offset + 8;
      dataSize = chunkSize;
      break;
    }

    offset += 8 + chunkSize;
  }

  if (dataOffset === 0 || dataSize === 0) {
    throw new Error("Invalid WAV format: data chunk not found");
  }

  // Extract PCM samples as Int16Array
  const bytesPerSample = bitsPerSample / 8;
  const numSamples = dataSize / bytesPerSample / numChannels;

  const leftChannel = new Int16Array(numSamples);
  const rightChannel = numChannels === 2 ? new Int16Array(numSamples) : leftChannel;

  for (let i = 0; i < numSamples; i++) {
    const sampleOffset = dataOffset + i * numChannels * bytesPerSample;
    leftChannel[i] = dataView.getInt16(sampleOffset, true);
    if (numChannels === 2) {
      rightChannel[i] = dataView.getInt16(sampleOffset + bytesPerSample, true);
    }
  }

  // Encode to MP3 using lamejs
  const mp3Encoder = new lamejs.Mp3Encoder(numChannels, sampleRate, 128);
  const mp3Chunks: Int8Array[] = [];
  const blockSize = 1152;

  for (let i = 0; i < numSamples; i += blockSize) {
    const leftBlock = leftChannel.subarray(i, i + blockSize);
    const rightBlock = numChannels === 2 ? rightChannel.subarray(i, i + blockSize) : undefined;

    const mp3Block =
      numChannels === 2
        ? mp3Encoder.encodeBuffer(leftBlock, rightBlock)
        : mp3Encoder.encodeBuffer(leftBlock);

    if (mp3Block.length > 0) {
      mp3Chunks.push(mp3Block);
    }
  }

  const finalBlock = mp3Encoder.flush();
  if (finalBlock.length > 0) {
    mp3Chunks.push(finalBlock);
  }

  // Combine all chunks into a single buffer
  const totalLength = mp3Chunks.reduce((sum, chunk) => sum + chunk.length, 0);
  const mp3Buffer = Buffer.alloc(totalLength);
  let position = 0;
  for (const chunk of mp3Chunks) {
    mp3Buffer.set(chunk, position);
    position += chunk.length;
  }

  return mp3Buffer;
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
