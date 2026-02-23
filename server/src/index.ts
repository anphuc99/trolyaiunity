import cors from "cors";
import fs from "fs";
import path from "path";
import express from "express";
import { fileURLToPath } from "url";
import "./env.js";
import { AppDataSource } from "./data-source.js";
import { createApiRouter } from "./routes/index.js";
import { embeddedClientAssets, embeddedClientIndexHtml } from "./embedded-client.js";

const DEFAULT_PORT = 4000;
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PUBLIC_DIR = path.join(__dirname, "..", "public");
const AVATARS_DIR = path.join(process.cwd(), "public", "avatars");
const AUDIO_DIR = path.join(process.cwd(), "data", "audio");
const CLIENT_DIST_CANDIDATES = [
  process.env.CLIENT_DIST_DIR,
  path.resolve(process.cwd(), "client", "dist"),
  path.resolve(process.cwd(), "public"),
  path.resolve(__dirname, "..", "..", "client", "dist")
].filter((candidate): candidate is string => Boolean(candidate));

const resolveClientIndex = () => {
  for (const candidate of CLIENT_DIST_CANDIDATES) {
    const indexHtml = path.join(candidate, "index.html");
    if (fs.existsSync(indexHtml)) {
      return { distDir: candidate, indexHtml };
    }
  }

  return null;
};

const hasEmbeddedClient = () =>
  Boolean(embeddedClientIndexHtml) && Object.keys(embeddedClientAssets).length > 0;

const sendEmbeddedAsset = (assetPath: string, res: express.Response) => {
  const asset = embeddedClientAssets[assetPath];

  if (!asset) {
    return false;
  }

  res.setHeader("Content-Type", asset.contentType);
  if (asset.encoding === "base64") {
    res.send(Buffer.from(asset.content, "base64"));
    return true;
  }

  res.send(asset.content);
  return true;
};

/**
 * Creates the Express application instance with default middleware and routes.
 *
 * @returns Configured Express application.
 */
const createApp = () => {
  const app = express();

  app.use(cors());

  app.use(express.json({ limit: "10mb" }));
  app.use(express.urlencoded({ limit: "10mb", extended: true }));

  app.use("/public", express.static(PUBLIC_DIR));
  app.use("/public/avatars", express.static(AVATARS_DIR));
  app.use("/audio", express.static(AUDIO_DIR));

  app.use("/api", createApiRouter(AppDataSource));

  if (hasEmbeddedClient()) {
    app.get("*", (req, res) => {
      const assetPath = req.path === "/" ? "index.html" : req.path.replace(/^\/+/, "");

      if (sendEmbeddedAsset(assetPath, res)) {
        return;
      }

      res.setHeader("Content-Type", "text/html; charset=utf-8");
      res.send(embeddedClientIndexHtml);
    });
  } else {
    const clientBuild = resolveClientIndex();
    if (clientBuild) {
      app.use(express.static(clientBuild.distDir));
      app.get("*", (_req, res) => {
        res.sendFile(clientBuild.indexHtml);
      });
    }
  }

  return app;
};

/**
 * Initializes the data source and starts the server.
 */
const startServer = async () => {
  try {
    await AppDataSource.initialize();
    const app = createApp();
    const port = Number(process.env.PORT ?? DEFAULT_PORT);

    app.listen(port, () => {
      console.log(`Server listening on http://localhost:${port}`);
    });
  } catch (error) {
    console.error("Failed to initialize the data source.", error);
    process.exitCode = 1;
  }
};

void startServer();
