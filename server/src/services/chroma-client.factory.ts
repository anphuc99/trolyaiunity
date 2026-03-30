import { ChromaClient } from "chromadb";

/**
 * Creates a ChromaDB client from a URL while avoiding deprecated `path` option.
 *
 * @param chromaUrl - Full ChromaDB URL (for example: http://localhost:8000).
 * @returns A configured ChromaClient instance.
 */
export const createChromaClient = (chromaUrl: string): ChromaClient => {
  const parsed = new URL(chromaUrl);
  const ssl = parsed.protocol === "https:";
  const host = parsed.hostname;
  const port = Number(parsed.port || (ssl ? 443 : 80));

  return new ChromaClient({ ssl, host, port });
};
