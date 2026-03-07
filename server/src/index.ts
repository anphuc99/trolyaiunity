import cors from "cors";
import path from "path";
import express from "express";
import { fileURLToPath } from "url";
import "./env.js";
import { AppDataSource } from "./data-source.js";
import { createApiRouter } from "./routes/index.js";

const DEFAULT_PORT = 4000;
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PUBLIC_DIR = path.join(__dirname, "..", "public");
const AVATARS_DIR = path.join(process.cwd(), "public", "avatars");

/**
 * Creates the Express application instance with default middleware and routes.
 *
 * @returns Configured Express application.
 */
const createApp = () => {
  const app = express();

  app.use(
    cors({
      origin: "*",
      methods: ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
      allowedHeaders: ["Content-Type", "Authorization", "X-Requested-With"]
    })
  );

  app.use(express.json({ limit: "10mb" }));
  app.use(express.urlencoded({ limit: "10mb", extended: true }));

  app.use("/public", express.static(PUBLIC_DIR));
  app.use("/public/avatars", express.static(AVATARS_DIR));

  app.use("/api", createApiRouter(AppDataSource));

  return app;
};

/**
 * Initializes the data source and starts the server.
 */
const startServer = async () => {
  try {
    await AppDataSource.initialize();
    console.log("Data source initialized successfully.");

    const app = createApp();
    const port = Number(process.env.PORT ?? DEFAULT_PORT);
    const host = process.env.HOST || "localhost";

    app.listen(port, host, () => {
      console.log(`MimiLearn server listening on http://${host}:${port}`);
    });
  } catch (error) {
    console.error("Failed to initialize the data source.", error);
    process.exitCode = 1;
  }
};

void startServer();
