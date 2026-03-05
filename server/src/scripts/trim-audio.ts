import { execFile } from "child_process";
import fs from "fs/promises";
import path from "path";
import ffmpegInstaller from "@ffmpeg-installer/ffmpeg";

const DEFAULT_SCAN_DIR = path.join(process.cwd(), "data", "audio");
const TRIM_SILENCE_THRESHOLD_DB = -45;
const TRIM_SILENCE_DURATION_SEC = 0.08;

const SUPPORTED_AUDIO_EXTENSIONS = new Set([
  ".mp3",
  ".wav",
  ".m4a",
  ".ogg",
  ".aac",
  ".flac",
  ".opus"
]);

/**
 * Recursively collects all audio files under the target folder.
 *
 * @param rootDir - Directory to scan.
 * @returns Absolute paths of audio files.
 */
const collectAudioFiles = async (rootDir: string): Promise<string[]> => {
  const entries = await fs.readdir(rootDir, { withFileTypes: true });
  const files: string[] = [];

  for (const entry of entries) {
    const fullPath = path.join(rootDir, entry.name);

    if (entry.isDirectory()) {
      files.push(...(await collectAudioFiles(fullPath)));
      continue;
    }

    if (!entry.isFile()) {
      continue;
    }

    const extension = path.extname(entry.name).toLowerCase();
    if (!SUPPORTED_AUDIO_EXTENSIONS.has(extension)) {
      continue;
    }

    files.push(fullPath);
  }

  return files;
};

/**
 * Trims leading/trailing silence for an audio file and replaces it in-place.
 *
 * @param filePath - Absolute path to source audio file.
 */
const trimAudioFileInPlace = async (filePath: string): Promise<void> => {
  const directory = path.dirname(filePath);
  const extension = path.extname(filePath);
  const baseName = path.basename(filePath, extension);
  const tempPath = path.join(directory, `${baseName}.__trim_tmp__${Date.now()}${extension}`);

  const filter = [
    `silenceremove=start_periods=1:start_duration=${TRIM_SILENCE_DURATION_SEC}:start_threshold=${TRIM_SILENCE_THRESHOLD_DB}dB`,
    "areverse",
    `silenceremove=start_periods=1:start_duration=${TRIM_SILENCE_DURATION_SEC}:start_threshold=${TRIM_SILENCE_THRESHOLD_DB}dB`,
    "areverse"
  ].join(",");

  try {
    await new Promise<void>((resolve, reject) => {
      execFile(
        ffmpegInstaller.path,
        ["-y", "-i", filePath, "-af", filter, tempPath],
        (error) => {
          if (error) {
            reject(new Error(`ffmpeg trim failed for '${filePath}': ${error.message}`));
            return;
          }

          resolve();
        }
      );
    });

    await fs.rename(tempPath, filePath);
  } finally {
    await fs.unlink(tempPath).catch(() => {});
  }
};

/**
 * Entrypoint for trimming all audio files.
 */
const run = async () => {
  const targetArg = process.argv[2]?.trim();
  const targetDir = targetArg ? path.resolve(process.cwd(), targetArg) : DEFAULT_SCAN_DIR;

  const exists = await fs.stat(targetDir).then(() => true).catch(() => false);
  if (!exists) {
    throw new Error(`Target directory does not exist: ${targetDir}`);
  }

  const files = await collectAudioFiles(targetDir);
  if (files.length === 0) {
    console.log(`[trim-audio] No audio files found in: ${targetDir}`);
    return;
  }

  console.log(`[trim-audio] Found ${files.length} audio file(s) in: ${targetDir}`);

  let success = 0;
  let failed = 0;

  for (const filePath of files) {
    try {
      await trimAudioFileInPlace(filePath);
      success += 1;
      console.log(`[trim-audio] OK: ${filePath}`);
    } catch (error) {
      failed += 1;
      console.error(`[trim-audio] FAIL: ${filePath}`);
      console.error(error instanceof Error ? error.message : String(error));
    }
  }

  console.log(`[trim-audio] Done. Success: ${success}, Failed: ${failed}`);

  if (failed > 0) {
    process.exitCode = 1;
  }
};

run().catch((error) => {
  console.error("[trim-audio] Fatal error:", error);
  process.exitCode = 1;
});
