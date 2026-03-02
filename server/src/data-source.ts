import "reflect-metadata";
import fs from "fs";
import path from "path";
import { DataSource } from "typeorm";
import CharacterEntity from "./models/character.entity.js";
import JournalEntity from "./models/journal.entity.js";
import LevelEntity from "./models/level.entity.js";
import MessageEntity from "./models/message.entity.js";
import StoryEntity from "./models/story.entity.js";
import StreakEntity from "./models/streak.entity.js";
import TranslationCardEntity from "./models/translation-card.entity.js";
import TranslationReviewEntity from "./models/translation-review.entity.js";
import UserEntity from "./models/user.entity.js";
import VocabularyEntity from "./models/vocabulary.entity.js";
import VocabularyMemoryEntity from "./models/vocabulary-memory.entity.js";
import VocabularyReviewEntity from "./models/vocabulary-review.entity.js";
import { repoRoot } from "./env.js";

type SupportedDbType = "mysql" | "sqlite";

/**
 * Normalizes DB type from env to a supported TypeORM driver.
 */
const normalizeDbType = (value: string | undefined): SupportedDbType => {
  const raw = (value ?? "mysql").trim().toLowerCase();
  return raw === "sqlite" ? "sqlite" : "mysql";
};

/**
 * Resolves a path that may be absolute or repo-root relative.
 */
const resolveRepoPath = (value: string) => {
  return path.isAbsolute(value) ? value : path.join(repoRoot, value);
};

/**
 * Reads a boolean env var.
 */
const readBool = (value: string | undefined, fallback: boolean) => {
  if (value == null) return fallback;
  return value.trim().toLowerCase() === "true";
};

/**
 * Builds TypeORM options for the selected database driver.
 */
const buildDatabaseOptions = (): DataSource["options"] => {
  const dbType = normalizeDbType(process.env.DB_TYPE);

  if (dbType === "sqlite") {
    const dbFile = resolveRepoPath(process.env.DB_SQLITE_PATH ?? "server/data/sqlite/mimi_chat.sqlite");

    fs.mkdirSync(path.dirname(dbFile), { recursive: true });

    return {
      type: "sqljs",
      location: dbFile,
      autoSave: true
    };
  }

  return {
    type: "mysql",
    host: process.env.DB_HOST ?? "localhost",
    port: Number(process.env.DB_PORT ?? 3306),
    username: process.env.DB_USER ?? "root",
    password: process.env.DB_PASSWORD ?? "",
    database: process.env.DB_NAME ?? "mimi_chat"
  };
};

/**
 * Shared TypeORM data source for the API server.
 */
export const AppDataSource = new DataSource({
  ...buildDatabaseOptions(),
  entities: [
    CharacterEntity,
    JournalEntity,
    LevelEntity,
    MessageEntity,
    StoryEntity,
    StreakEntity,
    TranslationCardEntity,
    TranslationReviewEntity,
    UserEntity,
    VocabularyEntity,
    VocabularyMemoryEntity,
    VocabularyReviewEntity
  ],
  synchronize: readBool(process.env.TYPEORM_SYNCHRONIZE, false),
  logging: readBool(process.env.TYPEORM_LOGGING, false)
});
