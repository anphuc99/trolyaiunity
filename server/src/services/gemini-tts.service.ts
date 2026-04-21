/**
 * Gemini TTS service with round-robin API key rotation.
 *
 * Uses the @google/generative-ai SDK (`gemini-2.5-flash-preview-tts`) to synthesise speech.
 * Four API keys (GEMINI_API_KEY_VOICE1 … GEMINI_API_KEY_VOICE4) are rotated in
 * sequence so that per-key rate limits are distributed evenly.
 */

import { GoogleGenerativeAI } from "@google/generative-ai";
import { createCheapAIService } from "./cheap-ai.service.js";

const GEMINI_TTS_MODEL = "gemini-3.1-flash-tts-preview";

// ---------------------------------------------------------------------------
// Round-robin key management
// ---------------------------------------------------------------------------

/** Lazily loaded API keys (populated on first call). */
const voiceKeys: string[] = [];

/** Current index into `voiceKeys`. */
let keyIndex = 0;

const RETRYABLE_STATUS_CODES = [400, 429, 500, 502, 503, 504];
const NO_AUDIO_ERROR_MARKER = "gemini tts returned no audio data";
const HANZI_REGEX = /[\u3400-\u9FFF\uF900-\uFAFF]/;

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

const getNextGeminiVoiceKeyEntry = (): { key: string; slot: number } => {
  loadKeys();

  if (voiceKeys.length === 0) {
    throw new Error("No GEMINI_API_KEY_VOICEn environment variables configured");
  }

  const slot = (keyIndex % voiceKeys.length) + 1;
  const key = voiceKeys[keyIndex % voiceKeys.length];
  keyIndex = (keyIndex + 1) % voiceKeys.length;

  return { key, slot };
};

const getConfiguredGeminiVoiceKeyCount = (): number => {
  loadKeys();
  return voiceKeys.length;
};

const isRetryableGeminiError = (error: unknown): boolean => {
  const message = error instanceof Error ? error.message : String(error ?? "");
  const normalizedMessage = message.toLowerCase();

  if (normalizedMessage.includes(NO_AUDIO_ERROR_MARKER)) {
    return true;
  }

  return RETRYABLE_STATUS_CODES.some((statusCode) => message.includes(`(${statusCode})`));
};

const isNoAudioGeminiError = (error: unknown): boolean => {
  const message = error instanceof Error ? error.message : String(error ?? "");
  return message.toLowerCase().includes(NO_AUDIO_ERROR_MARKER);
};

const delay = (ms: number): Promise<void> => new Promise((resolve) => setTimeout(resolve, ms));

const buildStyledPrompt = (text: string, tone?: string): string => {
  const trimmedTone = tone?.trim();
  if (!trimmedTone) {
    return text;
  }

  return `### DIRECTOR'S NOTES
Style: ${trimmedTone}

### TRANSCRIPT
${text}`;
};

const containsHanzi = (text: string): boolean => HANZI_REGEX.test(text);

const hasRetryableStatusCode = (error: unknown): boolean => {
  const message = error instanceof Error ? error.message : String(error ?? "");
  return RETRYABLE_STATUS_CODES.some((statusCode) => message.includes(`(${statusCode})`));
};

const shouldFallbackByError = (error: unknown): boolean => {
  const message = error instanceof Error ? error.message : String(error ?? "");
  return message.toLowerCase().includes(NO_AUDIO_ERROR_MARKER) || hasRetryableStatusCode(error);
};

type GeminiInlineData = {
  data?: string;
  mimeType?: string;
};

type GeminiCandidatePart = {
  inlineData?: GeminiInlineData;
  text?: string;
};

type GeminiGenerateResponseShape = {
  candidates?: Array<{
    finishReason?: string;
    content?: {
      parts?: GeminiCandidatePart[];
    };
  }>;
  promptFeedback?: {
    blockReason?: string;
  };
};

const extractInlineData = (response: GeminiGenerateResponseShape): GeminiInlineData | null => {
  const candidates = response.candidates ?? [];
  for (const candidate of candidates) {
    const parts = candidate.content?.parts ?? [];
    for (const part of parts) {
      const inline = part.inlineData;
      if (inline?.data) {
        return inline;
      }
    }
  }

  return null;
};

const buildNoAudioError = (response: GeminiGenerateResponseShape): Error => {
  const candidate = response.candidates?.[0];
  const finishReason = candidate?.finishReason ?? "unknown";
  const blockReason = response.promptFeedback?.blockReason ?? "none";
  const textFallback = candidate?.content?.parts?.map((part) => part.text ?? "").join(" ").trim() ?? "";

  return new Error(
    `Gemini TTS returned no audio data (finishReason=${finishReason}, blockReason=${blockReason}, textFallback=${textFallback || "empty"})`
  );
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

  const synthesizeWithPromptText = async (
    promptTextSource: string,
    resolveNoAudioFallbackPromptText?: () => Promise<string>
  ): Promise<Buffer> => {
    let lastError: unknown;

    for (let attempt = 0; attempt < maxAttempts; attempt += 1) {
      const { key: apiKey, slot: keySlot } = getNextGeminiVoiceKeyEntry();

      try {
        const genAI = new GoogleGenerativeAI(apiKey);
        const model = genAI.getGenerativeModel({ model: GEMINI_TTS_MODEL });

        const synthesizeWithModelPrompt = async (requestText: string): Promise<Buffer> => {
          const styledPrompt = buildStyledPrompt(requestText, tone);

          const buildRequest = (promptText: string) => ({
            contents: [{ role: "user", parts: [{ text: promptText }] }],
            generationConfig: {
              responseModalities: ["AUDIO"],
              speechConfig: {
                voiceConfig: {
                  prebuiltVoiceConfig: { voiceName }
                }
              }
            }
          });

          // @ts-expect-error — SDK typings may lag, but request shape is supported by Gemini API.
          const styledResult = await model.generateContent(buildRequest(styledPrompt));
          const styledResponse = styledResult.response as unknown as GeminiGenerateResponseShape;
          let inlineData = extractInlineData(styledResponse);

          // Fallback: when style instruction yields text-only response, retry same key with raw text.
          if (!inlineData && tone?.trim()) {
            // @ts-expect-error — SDK typings may lag, but request shape is supported by Gemini API.
            const plainResult = await model.generateContent(buildRequest(requestText));
            const plainResponse = plainResult.response as unknown as GeminiGenerateResponseShape;
            inlineData = extractInlineData(plainResponse);

            if (!inlineData) {
              throw buildNoAudioError(plainResponse);
            }
          }

          if (!inlineData) {
            throw buildNoAudioError(styledResponse);
          }

          const base64Audio = inlineData.data;
          if (!base64Audio) {
            throw buildNoAudioError(styledResponse);
          }

          const rawBuffer = Buffer.from(base64Audio, "base64");
          const mime: string = inlineData.mimeType ?? "";

          // Extract sample rate from mimeType if present (e.g. "audio/L16;rate=24000").
          const rateMatch = mime.match(/rate=(\d+)/);
          const sampleRate = rateMatch ? parseInt(rateMatch[1], 10) : 24000;

          // Raw PCM by explicit mimeType -> wrap in WAV container.
          if (mime.startsWith("audio/L16") || mime.startsWith("audio/pcm")) {
            return wrapPcmInWav(rawBuffer, sampleRate);
          }

          // Detect raw PCM by checking for missing RIFF/WAV header.
          // Gemini 3.1 Flash TTS returns raw PCM without a recognisable mimeType;
          // ffmpeg cannot process headerless PCM, so wrap it in a WAV container.
          const hasRiffHeader =
            rawBuffer.length >= 12 &&
            rawBuffer.subarray(0, 4).toString("ascii") === "RIFF" &&
            rawBuffer.subarray(8, 12).toString("ascii") === "WAVE";

          if (!hasRiffHeader) {
            return wrapPcmInWav(rawBuffer, sampleRate);
          }

          // Already a valid WAV (or another RIFF-based format ffmpeg can handle).
          return rawBuffer;
        };

        try {
          return await synthesizeWithModelPrompt(promptTextSource);
        } catch (initialError) {
          let effectiveError: unknown = initialError;

          if (resolveNoAudioFallbackPromptText && isNoAudioGeminiError(initialError)) {
            let fallbackPromptText = "";

            try {
              fallbackPromptText = (await resolveNoAudioFallbackPromptText())?.trim() ?? "";
            } catch (fallbackResolveError) {
              console.warn(
                `[GeminiTTS] failed to resolve no-audio fallback prompt: ${String(fallbackResolveError)}`
              );
            }

            if (fallbackPromptText && fallbackPromptText !== promptTextSource) {
              console.warn(`[GeminiTTS] no-audio on key slot ${keySlot}; retrying same key with pinyin fallback.`);
              try {
                return await synthesizeWithModelPrompt(fallbackPromptText);
              } catch (fallbackError) {
                effectiveError = fallbackError;
              }
            }
          }

          throw effectiveError;
        }
      } catch (error) {
        lastError = error;
        if (isNoAudioGeminiError(error)) {
          console.warn(
            `[GeminiTTS] no-audio response on key slot ${keySlot} (attempt ${attempt + 1}/${maxAttempts}).`
          );
        }

        const retryable = isRetryableGeminiError(error);
        if (!retryable || attempt >= maxAttempts - 1) {
          break;
        }

        await delay(120 * (attempt + 1));
      }
    }

    throw lastError instanceof Error ? lastError : new Error("Gemini TTS failed");
  };

  const inputText = text?.trim() ?? "";
  if (!inputText) {
    throw new Error("Gemini TTS requires non-empty input text");
  }

  let cachedPinyinText: string | null = null;
  const resolvePinyinText = async (): Promise<string> => {
    if (cachedPinyinText !== null) {
      return cachedPinyinText;
    }

    cachedPinyinText = "";
    if (!containsHanzi(inputText)) {
      return cachedPinyinText;
    }

    try {
      const cheapAI = createCheapAIService();
      cachedPinyinText = await cheapAI.transliterateChineseToPinyin(inputText);
    } catch (pinyinConvertError) {
      console.warn(`[GeminiTTS] failed to convert Hanzi to pinyin: ${String(pinyinConvertError)}`);
    }

    return cachedPinyinText;
  };

  try {
    return await synthesizeWithPromptText(inputText, resolvePinyinText);
  } catch (baseError) {
    if (!containsHanzi(inputText) || !shouldFallbackByError(baseError)) {
      throw baseError instanceof Error ? baseError : new Error("Gemini TTS failed");
    }

    const pinyinText = await resolvePinyinText();

    if (pinyinText && pinyinText !== inputText) {
      try {
        return await synthesizeWithPromptText(pinyinText);
      } catch (pinyinError) {
        if (!shouldFallbackByError(pinyinError)) {
          throw pinyinError instanceof Error ? pinyinError : new Error("Gemini TTS failed");
        }
      }
    }

    let ipaText = "";
    try {
      const cheapAI = createCheapAIService();
      ipaText = await cheapAI.transliterateChineseToIpa(inputText);
    } catch (ipaConvertError) {
      console.warn(`[GeminiTTS] failed to convert Hanzi to IPA: ${String(ipaConvertError)}`);
    }

    if (ipaText && ipaText !== inputText) {
      return synthesizeWithPromptText(ipaText);
    }

    throw baseError instanceof Error ? baseError : new Error("Gemini TTS failed");
  }
};
