import dotenv from "dotenv";
import fs from "fs";
import path from "path";

/**
 * Resolves the repository root directory.
 *
 * When running workspace scripts via npm workspaces, the current working directory may be either
 * the repo root or the `server/` folder.
 */
const resolveRepoRoot = () => {
  const cwd = process.cwd();
  return path.basename(cwd).toLowerCase() === "server" ? path.dirname(cwd) : cwd;
};

/**
 * Attempts to load environment variables from supported `.env` locations.
 *
 * Supported locations:
 * - `<repoRoot>/.env`
 * - `<repoRoot>/server/.env`
 */
const loadEnv = () => {
  const repoRoot = resolveRepoRoot();
  const candidates = [path.join(repoRoot, ".env"), path.join(repoRoot, "server", ".env")];
  const envPath = candidates.find((candidate) => fs.existsSync(candidate));

  if (envPath) {
    dotenv.config({ path: envPath });
  }

  return { envPath, repoRoot };
};

export const { envPath, repoRoot } = loadEnv();
