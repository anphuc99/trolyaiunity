/**
 * Gemini TTS service with round-robin API key rotation.
 *
 * Uses the @google/generative-ai SDK (`gemini-2.5-flash-preview-tts`) to synthesise speech.
 * Four API keys (GEMINI_API_KEY_VOICE1 … GEMINI_API_KEY_VOICE4) are rotated in
 * sequence so that per-key rate limits are distributed evenly.
 */

import { GoogleGenerativeAI } from "@google/generative-ai";

const GEMINI_TTS_MODEL = "gemini-2.5-flash-preview-tts";

// ---------------------------------------------------------------------------
// Round-robin key management
// ---------------------------------------------------------------------------

/** Lazily loaded API keys (populated on first call). */
const voiceKeys: string[] = [];

/** Current index into `voiceKeys`. */
let keyIndex = 0;

const RETRYABLE_STATUS_CODES = [429, 500, 502, 503, 504];

/**
 * Reads GEMINI_API_KEY_VOICE1 … GEMINI_API_KEY_VOICE4 from env once.
 */
const loadKeys = (): void => {
  if (voiceKeys.length > 0) {
    return;
  }

  for (let i = 1; i <= 4; i++) {
    const key = process.env[`GEMINI_API_KEY_VOICE${i}`]?.trim();
    if (key) {
      voiceKeys.push(key);
    }
  }
};

/**
 * Returns the next API key in round-robin order.
 *
 * @returns Gemini API key.
 * @throws If no keys are configured.
 */
export const getNextGeminiVoiceKey = (): string => {
  loadKeys();

  if (voiceKeys.length === 0) {
    throw new Error("No GEMINI_API_KEY_VOICEn environment variables configured");
  }

  const key = voiceKeys[keyIndex % voiceKeys.length];
  keyIndex = (keyIndex + 1) % voiceKeys.length;
  return key;
};

const getConfiguredGeminiVoiceKeyCount = (): number => {
  loadKeys();
  return voiceKeys.length;
};

const isRetryableGeminiError = (error: unknown): boolean => {
  const message = error instanceof Error ? error.message : String(error ?? "");
  return RETRYABLE_STATUS_CODES.some((statusCode) => message.includes(`(${statusCode})`));
};

const delay = (ms: number): Promise<void> => new Promise((resolve) => setTimeout(resolve, ms));

const buildStyledPrompt = (text: string, tone?: string): string => {
  const trimmedTone = tone?.trim();
  if (!trimmedTone) {
    return text;
  }

  return [
    "Read the following text aloud.",
    `Style instruction: ${trimmedTone}`,
    "Speak naturally and keep the original words unchanged.",
    `Text: ${text}`
  ].join("\n");
};

// ---------------------------------------------------------------------------
// WAV helper
// ---------------------------------------------------------------------------

/**
 * Wraps raw PCM 16-bit LE mono data into a standard WAV container so that
 * downstream ffmpeg processing works without extra input-format flags.
 *
 * @param pcm - Raw PCM buffer.
 * @param sampleRate - Sample rate in Hz.
 * @returns WAV buffer.
 */
const wrapPcmInWav = (pcm: Buffer, sampleRate: number): Buffer => {
  const header = Buffer.alloc(44);
  const dataSize = pcm.length;

  header.write("RIFF", 0);
  header.writeUInt32LE(36 + dataSize, 4);
  header.write("WAVE", 8);
  header.write("fmt ", 12);
  header.writeUInt32LE(16, 16); // fmt chunk size
  header.writeUInt16LE(1, 20); // PCM format
  header.writeUInt16LE(1, 22); // mono
  header.writeUInt32LE(sampleRate, 24);
  header.writeUInt32LE(sampleRate * 2, 28); // byte rate (sampleRate * channels * bitsPerSample / 8)
  header.writeUInt16LE(2, 32); // block align
  header.writeUInt16LE(16, 34); // bits per sample
  header.write("data", 36);
  header.writeUInt32LE(dataSize, 40);

  return Buffer.concat([header, pcm]);
};

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Calls the Gemini TTS API via the @google/generative-ai SDK and returns a WAV buffer.
 *
 * The API key is selected using round-robin rotation so rate limits across
 * the four configured keys are distributed evenly.
 *
 * @param text - Text to synthesise.
 * @param voiceName - Gemini prebuilt voice name (e.g. "Kore", "Puck").
 * @param tone - Optional style/tone instruction from client (e.g. "neutral, medium pitch").
 * @returns WAV audio buffer ready for ffmpeg post-processing.
 */
export const synthesizeGeminiTts = async (text: string, voiceName: string, tone?: string): Promise<Buffer> => {
  const maxAttempts = getConfiguredGeminiVoiceKeyCount();
  const prompt = buildStyledPrompt(text, tone);
  let lastError: unknown;

  for (let attempt = 0; attempt < maxAttempts; attempt += 1) {
    const apiKey = getNextGeminiVoiceKey();

    try {
      const genAI = new GoogleGenerativeAI(apiKey);
      const model = genAI.getGenerativeModel({
        model: GEMINI_TTS_MODEL,
        generationConfig: {
          // @ts-expect-error — SDK types lag behind API; responseModalities+speechConfig are valid at runtime.
          responseModalities: ["AUDIO"],
          speechConfig: {
            voiceConfig: {
              prebuiltVoiceConfig: { voiceName }
            }
          }
        }
      });

      const result = await model.generateContent(prompt);
      const response = result.response;
      const part = response.candidates?.[0]?.content?.parts?.[0];
      const inlineData = part?.inlineData;

      if (!inlineData?.data) {
        throw new Error("Gemini TTS returned no audio data");
      }

      const rawBuffer = Buffer.from(inlineData.data, "base64");
      const mime: string = inlineData.mimeType ?? "";

      // Raw PCM (audio/L16;rate=24000) -> wrap in WAV container.
      if (mime.startsWith("audio/L16") || mime.startsWith("audio/pcm")) {
        const rateMatch = mime.match(/rate=(\d+)/);
        const sampleRate = rateMatch ? parseInt(rateMatch[1], 10) : 24000;
        return wrapPcmInWav(rawBuffer, sampleRate);
      }

      // Already WAV or another format ffmpeg can handle.
      return rawBuffer;
    } catch (error) {
      lastError = error;
      const retryable = isRetryableGeminiError(error);
      if (!retryable || attempt >= maxAttempts - 1) {
        break;
      }

      await delay(120 * (attempt + 1));
    }
  }

  throw lastError instanceof Error ? lastError : new Error("Gemini TTS failed");
};
