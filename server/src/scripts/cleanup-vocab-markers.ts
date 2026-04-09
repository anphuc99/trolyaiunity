/**
 * Migration Script: remove legacy vocabulary markers from stored message content.
 *
 * Old chat logic could wrap vocabulary items in double asterisks (example: 我**爱**你).
 * This script removes those marker characters from persisted database content.
 *
 * Targets:
 * - messages.content
 * - my_log_messages.content
 * - translation_cards.content
 *
 * User-authored rows (characterName = "User") are skipped to avoid rewriting user input.
 *
 * Usage:
 *   npm --prefix server run db:cleanup:vocab-markers
 *   npm --prefix server run db:cleanup:vocab-markers -- --dry-run
 */

import { type Repository } from "typeorm";
import { AppDataSource } from "../data-source.js";
import MessageEntity from "../models/message.entity.js";
import MyLogMessageEntity from "../models/my-log-message.entity.js";
import TranslationCardEntity from "../models/translation-card.entity.js";

interface CleanupStats {
  candidates: number;
  skippedUserRows: number;
  updatedRows: number;
}

interface MessageLikeEntity {
  content: string;
  characterName: string;
}

const USER_SPEAKER = "user";
const MARKER = "**";

const normalizeSpeaker = (value: string) => value.trim().toLowerCase();

const stripVocabularyMarkers = (value: string) => {
  if (!value.includes(MARKER)) {
    return value;
  }

  return value.replace(/\*\*/g, "");
};

const cleanupRepository = async <T extends MessageLikeEntity>(
  repository: Repository<T>,
  label: string,
  dryRun: boolean
): Promise<CleanupStats> => {
  const candidates = await repository
    .createQueryBuilder("row")
    .where("row.content LIKE :pattern", { pattern: `%${MARKER}%` })
    .getMany();

  let skippedUserRows = 0;
  let updatedRows = 0;
  const rowsToPersist: T[] = [];

  for (const row of candidates) {
    if (normalizeSpeaker(row.characterName) === USER_SPEAKER) {
      skippedUserRows += 1;
      continue;
    }

    const cleanedContent = stripVocabularyMarkers(row.content);
    if (cleanedContent === row.content) {
      continue;
    }

    updatedRows += 1;
    if (!dryRun) {
      row.content = cleanedContent;
      rowsToPersist.push(row);
    }
  }

  if (!dryRun && rowsToPersist.length > 0) {
    await repository.save(rowsToPersist, { chunk: 200 });
  }

  console.log(
    `[${label}] candidates=${candidates.length}, skippedUserRows=${skippedUserRows}, ${dryRun ? "wouldUpdate" : "updated"}=${updatedRows}`
  );

  return {
    candidates: candidates.length,
    skippedUserRows,
    updatedRows
  };
};

const main = async () => {
  const dryRun = process.argv.includes("--dry-run");

  console.log(`Starting vocabulary marker cleanup (${dryRun ? "dry-run" : "apply"})...`);

  await AppDataSource.initialize();

  try {
    const messageStats = await cleanupRepository(AppDataSource.getRepository(MessageEntity), "messages", dryRun);
    const myLogMessageStats = await cleanupRepository(AppDataSource.getRepository(MyLogMessageEntity), "my_log_messages", dryRun);
    const translationCardStats = await cleanupRepository(
      AppDataSource.getRepository(TranslationCardEntity),
      "translation_cards",
      dryRun
    );

    const totalCandidates = messageStats.candidates + myLogMessageStats.candidates + translationCardStats.candidates;
    const totalSkippedUserRows =
      messageStats.skippedUserRows + myLogMessageStats.skippedUserRows + translationCardStats.skippedUserRows;
    const totalUpdatedRows = messageStats.updatedRows + myLogMessageStats.updatedRows + translationCardStats.updatedRows;

    console.log("Cleanup summary:");
    console.log(`- totalCandidates=${totalCandidates}`);
    console.log(`- totalSkippedUserRows=${totalSkippedUserRows}`);
    console.log(`- ${dryRun ? "totalWouldUpdateRows" : "totalUpdatedRows"}=${totalUpdatedRows}`);
  } finally {
    if (AppDataSource.isInitialized) {
      await AppDataSource.destroy();
    }
  }
};

main().catch((error) => {
  console.error("Vocabulary marker cleanup failed:", error);
  process.exit(1);
});
