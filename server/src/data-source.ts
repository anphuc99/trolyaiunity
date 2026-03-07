import "reflect-metadata";
import path from "path";
import { DataSource } from "typeorm";
import CharacterEntity from "./models/character.entity.js";
import JournalEntity from "./models/journal.entity.js";
import KnowledgeEntity from "./models/knowledge.entity.js";
import KnowledgeReviewEntity from "./models/knowledge-review.entity.js";
import MessageEntity from "./models/message.entity.js";
import SubjectEntity from "./models/subject.entity.js";
import UserEntity from "./models/user.entity.js";
import { repoRoot } from "./env.js";

type SupportedDbType = "mysql";

/**
 * Normalizes DB type from env to a supported TypeORM driver.
 */
const normalizeDbType = (value: string | undefined): SupportedDbType => {
  const raw = (value ?? "mysql").trim().toLowerCase();
  if (raw !== "mysql") {
    console.warn(`Unsupported DB_TYPE "${raw}", falling back to mysql.`);
  }
  return "mysql";
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
 * Builds TypeORM options for the MySQL driver.
 */
const buildDatabaseOptions = (): DataSource["options"] => {
  return {
    type: "mysql",
    host: process.env.DB_HOST ?? "localhost",
    port: Number(process.env.DB_PORT ?? 3306),
    username: process.env.DB_USER ?? "root",
    password: process.env.DB_PASSWORD ?? "",
    database: process.env.DB_NAME ?? "mimi_learn"
  };
};

/**
 * Shared TypeORM data source for the MimiLearn API server.
 */
export const AppDataSource = new DataSource({
  ...buildDatabaseOptions(),
  entities: [
    CharacterEntity,
    JournalEntity,
    KnowledgeEntity,
    KnowledgeReviewEntity,
    MessageEntity,
    SubjectEntity,
    UserEntity
  ],
  synchronize: readBool(process.env.TYPEORM_SYNCHRONIZE, false),
  logging: readBool(process.env.TYPEORM_LOGGING, false)
});
