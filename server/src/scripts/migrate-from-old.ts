/**
 * Migration Script: Port data from old MimiChat (JSON files) to new MimiChat (TypeORM/MySQL)
 *
 * Usage:
 *   npm run migrate:old -- --source <path-to-old-server-data>
 *
 * Example:
 *   npm run migrate:old -- --source D:/Unity/mimichat/server/data
 *
 * This script migrates:
 * - Stories (stories-index.json + stories/*.json)
 * - Vocabularies (vocabulary-store.json)
 * - Vocabulary Reviews (vocabulary-store.json)
 * - Vocabulary Memories (vocabulary-store.json)
 * - Characters (data.json)
 * - Journals and Messages (stories/*.json)
 * - Translation Cards and Reviews (translation-store.json)
 * - Streak data (streak.json)
 *
 * NOTE: Since the old code has no user system, all records are assigned userId = 1.
 */

import fs from "fs";
import path from "path";
import bcrypt from "bcryptjs";
import { AppDataSource } from "../data-source.js";
import VocabularyEntity from "../models/vocabulary.entity.js";
import VocabularyReviewEntity from "../models/vocabulary-review.entity.js";
import VocabularyMemoryEntity from "../models/vocabulary-memory.entity.js";
import CharacterEntity from "../models/character.entity.js";
import JournalEntity from "../models/journal.entity.js";
import MessageEntity from "../models/message.entity.js";
import TranslationCardEntity from "../models/translation-card.entity.js";
import TranslationReviewEntity from "../models/translation-review.entity.js";
import StreakEntity from "../models/streak.entity.js";
import StoryEntity from "../models/story.entity.js";
import UserEntity from "../models/user.entity.js";

/** Default user ID since old code has no user system */
const DEFAULT_USER_ID = 1;

// ============================================================================
// Old Data Type Definitions (from mimichat/types.ts)
// ============================================================================

interface OldVocabularyItem {
  id: string;
  korean: string;
  vietnamese: string;
  storyId?: string;
  dailyChatId?: string;
  createdDate?: string;
  isManuallyAdded?: boolean;
}

interface OldReviewHistoryEntry {
  date: string;
  correctCount: number;
  incorrectCount: number;
  intervalBefore: number;
  intervalAfter: number;
  rating?: number;
  stabilityBefore?: number;
  stabilityAfter?: number;
  difficultyBefore?: number;
  difficultyAfter?: number;
  retrievability?: number;
}

interface OldVocabularyReview {
  vocabularyId: string;
  dailyChatId: string;
  currentIntervalDays: number;
  nextReviewDate: string;
  lastReviewDate: string | null;
  reviewHistory: OldReviewHistoryEntry[];
  stability?: number;
  difficulty?: number;
  lapses?: number;
  cardDirection?: "kr-vn" | "vn-kr";
  isStarred?: boolean;
  storyId?: string;
}

interface OldVocabularyMemory {
  vocabularyId: string;
  userMemory: string;
  linkedMessageIds: string[];
  createdDate: string;
  updatedDate?: string;
  storyId?: string;
}

interface OldVocabularyStore {
  vocabularies: OldVocabularyItem[];
  reviews: OldVocabularyReview[];
  memories: OldVocabularyMemory[];
  progress: Record<string, unknown>;
  lastUpdated?: string;
}

interface OldRelationInfo {
  opinion: string;
  sentiment?: "positive" | "neutral" | "negative";
  closeness?: number;
}

interface OldCharacter {
  id: string;
  name: string;
  personality: string;
  appearance?: string;
  gender: "male" | "female";
  voiceName?: string;
  pitch?: number;
  speakingRate?: number;
  avatar?: string;
  relations?: Record<string, OldRelationInfo>;
  userOpinion?: OldRelationInfo;
}

interface OldMessage {
  id: string;
  text: string;
  sender: "user" | "bot";
  characterName?: string;
  audioData?: string;
  translation?: string;
  rawText?: string;
}

interface OldDailyChat {
  id?: string;
  date: string;
  summary: string;
  messages: OldMessage[];
}

interface OldDataJson {
  version: number;
  journal: OldDailyChat[];
  characters: OldCharacter[];
  activeCharacterIds?: string[];
  context?: string;
  currentLevel?: string;
}

interface OldTranslationCard {
  messageId: string;
  dailyChatId: string;
  dailyChatDate: string;
  dailyChatSummary?: string;
  characterName: string;
  text: string;
  translation?: string;
  audioData?: string;
  createdAt?: string;
}

interface OldTranslationReview {
  vocabularyId: string; // Actually messageId in old code
  dailyChatId: string;
  currentIntervalDays: number;
  nextReviewDate: string;
  lastReviewDate: string | null;
  reviewHistory: OldReviewHistoryEntry[];
  stability?: number;
  difficulty?: number;
  lapses?: number;
  isStarred?: boolean;
}

interface OldTranslationStore {
  version: number;
  reviews: OldTranslationReview[];
  cards: OldTranslationCard[];
}

interface OldStreakJson {
  currentStreak: number;
  longestStreak: number;
  lastActivityDate: string | null;
}

interface OldStoryMeta {
  id: string;
  name: string;
  createdAt: string;
  updatedAt: string;
  charactersPreview: string[];
  messageCount: number;
}

interface OldStoriesIndex {
  stories: OldStoryMeta[];
  lastOpenedStoryId?: string;
}

interface OldStoryFile {
  version: number;
  journal: OldDailyChat[];
  characters: OldCharacter[];
  storyPlot?: string;
  context?: string;
  relatedJournalIds?: string[];
}

// ============================================================================
// Migration Statistics
// ============================================================================

interface MigrationStats {
  stories: { total: number; success: number; skipped: number; errors: string[] };
  vocabularies: { total: number; success: number; skipped: number; errors: string[] };
  vocabularyReviews: { total: number; success: number; skipped: number; errors: string[] };
  vocabularyMemories: { total: number; success: number; skipped: number; errors: string[] };
  characters: { total: number; success: number; skipped: number; errors: string[] };
  journals: { total: number; success: number; skipped: number; errors: string[] };
  messages: { total: number; success: number; skipped: number; errors: string[] };
  translationCards: { total: number; success: number; skipped: number; errors: string[] };
  translationReviews: { total: number; success: number; skipped: number; errors: string[] };
  streak: { total: number; success: number; skipped: number; errors: string[] };
}

const createEmptyStats = (): MigrationStats => ({
  stories: { total: 0, success: 0, skipped: 0, errors: [] },
  vocabularies: { total: 0, success: 0, skipped: 0, errors: [] },
  vocabularyReviews: { total: 0, success: 0, skipped: 0, errors: [] },
  vocabularyMemories: { total: 0, success: 0, skipped: 0, errors: [] },
  characters: { total: 0, success: 0, skipped: 0, errors: [] },
  journals: { total: 0, success: 0, skipped: 0, errors: [] },
  messages: { total: 0, success: 0, skipped: 0, errors: [] },
  translationCards: { total: 0, success: 0, skipped: 0, errors: [] },
  translationReviews: { total: 0, success: 0, skipped: 0, errors: [] },
  streak: { total: 0, success: 0, skipped: 0, errors: [] }
});

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Reads and parses a JSON file safely.
 */
function readJsonFile<T>(filePath: string): T | null {
  try {
    if (!fs.existsSync(filePath)) {
      console.warn(`⚠️ File not found: ${filePath}`);
      return null;
    }
    const content = fs.readFileSync(filePath, "utf-8");
    return JSON.parse(content) as T;
  } catch (err) {
    console.error(`❌ Failed to read/parse ${filePath}:`, err);
    return null;
  }
}

/**
 * Parses a date string to Date object safely.
 */
function parseDate(dateStr: string | null | undefined): Date | null {
  if (!dateStr) return null;
  const d = new Date(dateStr);
  return isNaN(d.getTime()) ? null : d;
}

/**
 * Extracts tone from rawText if available.
 * e.g., "Tone: cheerfully" -> "cheerfully"
 */
function extractTone(rawText?: string): string | null {
  if (!rawText) return null;
  const match = rawText.match(/Tone:\s*(.+?)$/m);
  return match ? match[1].trim() : null;
}

// ============================================================================
// Migration Functions
// ============================================================================

/**
 * Ensures a default user exists for migration (since old code has no user system).
 * Creates user with ID=1 if not exists.
 */
async function ensureDefaultUser(): Promise<void> {
  const userRepo = AppDataSource.getRepository(UserEntity);

  const existingUser = await userRepo.findOne({ where: { id: DEFAULT_USER_ID } });
  if (existingUser) {
    console.log("   ✅ Default user already exists (ID: 1)");
    return;
  }

  // Create a default user for migration
  const passwordHash = await bcrypt.hash("migrated_user_password", 10);
  const newUser = new UserEntity();
  newUser.username = "migrated_user";
  newUser.passwordHash = passwordHash;

  await userRepo.save(newUser);
  console.log("   ✅ Default user created (ID: 1, username: migrated_user)");
}

/**
 * Migrates vocabularies from vocabulary-store.json.
 */
async function migrateVocabularies(
  vocabStore: OldVocabularyStore,
  stats: MigrationStats
): Promise<Map<string, string>> {
  console.log("\n📚 Migrating vocabularies...");
  const vocabRepo = AppDataSource.getRepository(VocabularyEntity);
  const idMap = new Map<string, string>(); // old ID -> new ID (same in this case)

  stats.vocabularies.total = vocabStore.vocabularies.length;

  for (const oldVocab of vocabStore.vocabularies) {
    try {
      // Check if already exists
      const existing = await vocabRepo.findOne({ where: { id: oldVocab.id } });
      if (existing) {
        stats.vocabularies.skipped++;
        idMap.set(oldVocab.id, oldVocab.id);
        continue;
      }

      const newVocab = new VocabularyEntity();
      newVocab.id = oldVocab.id;
      newVocab.korean = oldVocab.korean;
      newVocab.vietnamese = oldVocab.vietnamese;
      newVocab.isManuallyAdded = oldVocab.isManuallyAdded ?? false;
      newVocab.userId = DEFAULT_USER_ID;

      await vocabRepo.save(newVocab);
      idMap.set(oldVocab.id, oldVocab.id);
      stats.vocabularies.success++;
    } catch (err) {
      const msg = `Vocab ${oldVocab.id}: ${err instanceof Error ? err.message : String(err)}`;
      stats.vocabularies.errors.push(msg);
    }
  }

  console.log(`   ✅ ${stats.vocabularies.success} migrated, ${stats.vocabularies.skipped} skipped`);
  return idMap;
}

/**
 * Migrates vocabulary reviews from vocabulary-store.json.
 */
async function migrateVocabularyReviews(
  vocabStore: OldVocabularyStore,
  vocabIdMap: Map<string, string>,
  stats: MigrationStats
): Promise<void> {
  console.log("\n📖 Migrating vocabulary reviews...");
  const reviewRepo = AppDataSource.getRepository(VocabularyReviewEntity);

  stats.vocabularyReviews.total = vocabStore.reviews.length;

  for (const oldReview of vocabStore.reviews) {
    try {
      // Skip if vocabulary doesn't exist
      if (!vocabIdMap.has(oldReview.vocabularyId)) {
        stats.vocabularyReviews.skipped++;
        continue;
      }

      // Check if already exists
      const existing = await reviewRepo.findOne({
        where: { vocabularyId: oldReview.vocabularyId }
      });
      if (existing) {
        stats.vocabularyReviews.skipped++;
        continue;
      }

      const nextReviewDate = parseDate(oldReview.nextReviewDate);
      if (!nextReviewDate) {
        stats.vocabularyReviews.errors.push(`Review for ${oldReview.vocabularyId}: Invalid nextReviewDate`);
        continue;
      }

      const newReview = new VocabularyReviewEntity();
      newReview.vocabularyId = oldReview.vocabularyId;
      newReview.stability = oldReview.stability ?? 0;
      newReview.difficulty = oldReview.difficulty ?? 5;
      newReview.lapses = oldReview.lapses ?? 0;
      newReview.currentIntervalDays = oldReview.currentIntervalDays;
      newReview.nextReviewDate = nextReviewDate;
      newReview.lastReviewDate = parseDate(oldReview.lastReviewDate);
      newReview.cardDirection = oldReview.cardDirection ?? "kr-vn";
      newReview.isStarred = oldReview.isStarred ?? false;
      newReview.reviewHistoryJson = JSON.stringify(oldReview.reviewHistory || []);
      newReview.userId = DEFAULT_USER_ID;

      await reviewRepo.save(newReview);
      stats.vocabularyReviews.success++;
    } catch (err) {
      const msg = `Review ${oldReview.vocabularyId}: ${err instanceof Error ? err.message : String(err)}`;
      stats.vocabularyReviews.errors.push(msg);
    }
  }

  console.log(`   ✅ ${stats.vocabularyReviews.success} migrated, ${stats.vocabularyReviews.skipped} skipped`);
}

/**
 * Migrates vocabulary memories from vocabulary-store.json.
 */
async function migrateVocabularyMemories(
  vocabStore: OldVocabularyStore,
  vocabIdMap: Map<string, string>,
  stats: MigrationStats
): Promise<void> {
  console.log("\n🧠 Migrating vocabulary memories...");
  const memoryRepo = AppDataSource.getRepository(VocabularyMemoryEntity);

  stats.vocabularyMemories.total = vocabStore.memories.length;

  for (const oldMemory of vocabStore.memories) {
    try {
      // Skip if vocabulary doesn't exist
      if (!vocabIdMap.has(oldMemory.vocabularyId)) {
        stats.vocabularyMemories.skipped++;
        continue;
      }

      // Check if already exists
      const existing = await memoryRepo.findOne({
        where: { vocabularyId: oldMemory.vocabularyId }
      });
      if (existing) {
        stats.vocabularyMemories.skipped++;
        continue;
      }

      const newMemory = new VocabularyMemoryEntity();
      newMemory.vocabularyId = oldMemory.vocabularyId;
      newMemory.userMemory = oldMemory.userMemory;
      newMemory.linkedMessageIdsJson = JSON.stringify(oldMemory.linkedMessageIds || []);
      newMemory.userId = DEFAULT_USER_ID;

      await memoryRepo.save(newMemory);
      stats.vocabularyMemories.success++;
    } catch (err) {
      const msg = `Memory ${oldMemory.vocabularyId}: ${err instanceof Error ? err.message : String(err)}`;
      stats.vocabularyMemories.errors.push(msg);
    }
  }

  console.log(`   ✅ ${stats.vocabularyMemories.success} migrated, ${stats.vocabularyMemories.skipped} skipped`);
}

/**
 * Migrates characters from data.json.
 */
async function migrateCharacters(
  dataJson: OldDataJson,
  stats: MigrationStats
): Promise<Map<string, number>> {
  console.log("\n👤 Migrating characters...");
  const charRepo = AppDataSource.getRepository(CharacterEntity);
  const idMap = new Map<string, number>(); // old string ID -> new numeric ID

  stats.characters.total = dataJson.characters.length;

  for (const oldChar of dataJson.characters) {
    try {
      // Check if already exists by name (since old IDs are string and new are numeric)
      const existing = await charRepo.findOne({
        where: { name: oldChar.name, userId: DEFAULT_USER_ID }
      });
      if (existing) {
        stats.characters.skipped++;
        idMap.set(oldChar.id, existing.id);
        continue;
      }

      const newChar = new CharacterEntity();
      newChar.name = oldChar.name;
      newChar.personality = oldChar.personality;
      newChar.gender = oldChar.gender;
      newChar.appearance = oldChar.appearance ?? null;
      newChar.avatar = oldChar.avatar ?? null;
      newChar.voiceName = oldChar.voiceName ?? null;
      newChar.pitch = oldChar.pitch ?? null;
      newChar.speakingRate = oldChar.speakingRate ?? null;
      newChar.userId = DEFAULT_USER_ID;

      const saved = await charRepo.save(newChar);
      idMap.set(oldChar.id, saved.id);
      stats.characters.success++;
    } catch (err) {
      const msg = `Character ${oldChar.name}: ${err instanceof Error ? err.message : String(err)}`;
      stats.characters.errors.push(msg);
    }
  }

  console.log(`   ✅ ${stats.characters.success} migrated, ${stats.characters.skipped} skipped`);
  return idMap;
}

/**
 * Migrates journals and messages from data.json.
 * Returns a map of (old dailyChatId/date) -> new journalId for translation card reference.
 */
async function migrateJournalsAndMessages(
  dataJson: OldDataJson,
  stats: MigrationStats
): Promise<Map<string, number>> {
  console.log("\n📔 Migrating journals and messages...");
  const journalRepo = AppDataSource.getRepository(JournalEntity);
  const messageRepo = AppDataSource.getRepository(MessageEntity);
  const journalIdMap = new Map<string, number>(); // date or dailyChatId -> new journal ID

  stats.journals.total = dataJson.journal.length;

  for (const oldJournal of dataJson.journal) {
    try {
      // Use date as identifier since old format may not have id
      const identifier = oldJournal.id || oldJournal.date;

      // Check if already exists by summary date match
      const existing = await journalRepo
        .createQueryBuilder("j")
        .where("j.user_id = :userId", { userId: DEFAULT_USER_ID })
        .andWhere("DATE(j.created_at) = :date", { date: oldJournal.date })
        .getOne();

      if (existing) {
        stats.journals.skipped++;
        journalIdMap.set(identifier, existing.id);
        journalIdMap.set(oldJournal.date, existing.id);
        continue;
      }

      const newJournal = new JournalEntity();
      newJournal.summary = oldJournal.summary;
      newJournal.userId = DEFAULT_USER_ID;

      const savedJournal = await journalRepo.save(newJournal);
      journalIdMap.set(identifier, savedJournal.id);
      journalIdMap.set(oldJournal.date, savedJournal.id);
      stats.journals.success++;

      // Migrate messages for this journal
      stats.messages.total += oldJournal.messages.length;
      for (const oldMsg of oldJournal.messages) {
        try {
          // Skip user messages (only bot messages have meaningful content for drill)
          // But we still want to preserve them for history
          const existing = await messageRepo.findOne({ where: { id: oldMsg.id } });
          if (existing) {
            stats.messages.skipped++;
            continue;
          }

          // Only migrate bot messages (characterName is present)
          if (oldMsg.sender === "bot" && oldMsg.characterName) {
            const newMsg = new MessageEntity();
            newMsg.id = oldMsg.id;
            newMsg.content = oldMsg.text;
            newMsg.characterName = oldMsg.characterName;
            newMsg.translation = oldMsg.translation ?? null;
            newMsg.tone = extractTone(oldMsg.rawText);
            newMsg.audio = oldMsg.audioData ?? null;
            newMsg.userId = DEFAULT_USER_ID;
            newMsg.journalId = savedJournal.id;

            await messageRepo.save(newMsg);
            stats.messages.success++;
          } else {
            stats.messages.skipped++;
          }
        } catch (err) {
          const msg = `Message ${oldMsg.id}: ${err instanceof Error ? err.message : String(err)}`;
          stats.messages.errors.push(msg);
        }
      }
    } catch (err) {
      const msg = `Journal ${oldJournal.date}: ${err instanceof Error ? err.message : String(err)}`;
      stats.journals.errors.push(msg);
    }
  }

  console.log(`   ✅ Journals: ${stats.journals.success} migrated, ${stats.journals.skipped} skipped`);
  console.log(`   ✅ Messages: ${stats.messages.success} migrated, ${stats.messages.skipped} skipped`);
  return journalIdMap;
}

/**
 * Migrates translation cards and reviews from translation-store.json.
 */
async function migrateTranslationData(
  translationStore: OldTranslationStore,
  journalIdMap: Map<string, number>,
  stats: MigrationStats
): Promise<void> {
  console.log("\n🔤 Migrating translation cards and reviews...");
  const cardRepo = AppDataSource.getRepository(TranslationCardEntity);
  const reviewRepo = AppDataSource.getRepository(TranslationReviewEntity);

  // Map messageId -> card ID for review linking
  const cardIdMap = new Map<string, number>();

  stats.translationCards.total = translationStore.cards.length;

  for (const oldCard of translationStore.cards) {
    try {
      // Check if already exists
      const existing = await cardRepo.findOne({
        where: { messageId: oldCard.messageId, userId: DEFAULT_USER_ID }
      });
      if (existing) {
        stats.translationCards.skipped++;
        cardIdMap.set(oldCard.messageId, existing.id);
        continue;
      }

      // Find journal ID from dailyChatId or date
      let journalId = journalIdMap.get(oldCard.dailyChatId);
      if (!journalId) {
        journalId = journalIdMap.get(oldCard.dailyChatDate);
      }
      if (!journalId) {
        // Create a placeholder journal if not found
        const journalRepo = AppDataSource.getRepository(JournalEntity);
        const placeholder = new JournalEntity();
        placeholder.summary = oldCard.dailyChatSummary || `Imported from ${oldCard.dailyChatDate}`;
        placeholder.userId = DEFAULT_USER_ID;
        const saved = await journalRepo.save(placeholder);
        journalId = saved.id;
        journalIdMap.set(oldCard.dailyChatId, journalId);
      }

      const newCard = new TranslationCardEntity();
      newCard.messageId = oldCard.messageId;
      newCard.content = oldCard.text;
      newCard.translation = oldCard.translation ?? null;
      newCard.characterName = oldCard.characterName;
      newCard.audio = oldCard.audioData ?? null;
      newCard.journalId = journalId;
      newCard.userId = DEFAULT_USER_ID;

      const savedCard = await cardRepo.save(newCard);
      cardIdMap.set(oldCard.messageId, savedCard.id);
      stats.translationCards.success++;
    } catch (err) {
      const msg = `TranslationCard ${oldCard.messageId}: ${err instanceof Error ? err.message : String(err)}`;
      stats.translationCards.errors.push(msg);
    }
  }

  console.log(`   ✅ Cards: ${stats.translationCards.success} migrated, ${stats.translationCards.skipped} skipped`);

  // Migrate translation reviews
  stats.translationReviews.total = translationStore.reviews.length;

  for (const oldReview of translationStore.reviews) {
    try {
      // In old code, vocabularyId is actually the messageId
      const cardId = cardIdMap.get(oldReview.vocabularyId);
      if (!cardId) {
        stats.translationReviews.skipped++;
        continue;
      }

      // Check if already exists
      const existing = await reviewRepo.findOne({
        where: { translationCardId: cardId, userId: DEFAULT_USER_ID }
      });
      if (existing) {
        stats.translationReviews.skipped++;
        continue;
      }

      const nextReviewDate = parseDate(oldReview.nextReviewDate);
      if (!nextReviewDate) {
        stats.translationReviews.errors.push(`TranslationReview ${oldReview.vocabularyId}: Invalid nextReviewDate`);
        continue;
      }

      const newReview = new TranslationReviewEntity();
      newReview.translationCardId = cardId;
      newReview.userId = DEFAULT_USER_ID;
      newReview.stability = oldReview.stability ?? 0;
      newReview.difficulty = oldReview.difficulty ?? 5;
      newReview.lapses = oldReview.lapses ?? 0;
      newReview.currentIntervalDays = oldReview.currentIntervalDays;
      newReview.nextReviewDate = nextReviewDate;
      newReview.lastReviewDate = parseDate(oldReview.lastReviewDate);
      newReview.reviewHistoryJson = JSON.stringify(oldReview.reviewHistory || []);
      newReview.isStarred = oldReview.isStarred ?? false;

      await reviewRepo.save(newReview);
      stats.translationReviews.success++;
    } catch (err) {
      const msg = `TranslationReview ${oldReview.vocabularyId}: ${err instanceof Error ? err.message : String(err)}`;
      stats.translationReviews.errors.push(msg);
    }
  }

  console.log(`   ✅ Reviews: ${stats.translationReviews.success} migrated, ${stats.translationReviews.skipped} skipped`);
}

/**
 * Migrates streak data from streak.json.
 */
async function migrateStreak(
  streakJson: OldStreakJson,
  stats: MigrationStats
): Promise<void> {
  console.log("\n🔥 Migrating streak...");
  const streakRepo = AppDataSource.getRepository(StreakEntity);

  stats.streak.total = 1;

  try {
    // Check if already exists
    const existing = await streakRepo.findOne({
      where: { userId: DEFAULT_USER_ID }
    });
    if (existing) {
      stats.streak.skipped++;
      console.log(`   ⏭️ Streak already exists, skipping`);
      return;
    }

    const newStreak = new StreakEntity();
    newStreak.userId = DEFAULT_USER_ID;
    newStreak.currentStreak = streakJson.currentStreak;
    newStreak.longestStreak = streakJson.longestStreak;
    newStreak.lastCompletedDate = parseDate(streakJson.lastActivityDate);

    await streakRepo.save(newStreak);
    stats.streak.success++;
    console.log(`   ✅ Streak migrated: ${streakJson.currentStreak} days (longest: ${streakJson.longestStreak})`);
  } catch (err) {
    const msg = `Streak: ${err instanceof Error ? err.message : String(err)}`;
    stats.streak.errors.push(msg);
    console.error(`   ❌ ${msg}`);
  }
}

/**
 * Migrates stories from stories-index.json and individual story files.
 * Returns a map of old storyId -> new storyId for journal linking.
 */
async function migrateStories(
  sourcePath: string,
  stats: MigrationStats
): Promise<{ storyIdMap: Map<string, number>; journalIdMap: Map<string, number> }> {
  console.log("\n📖 Migrating stories...");
  const storyRepo = AppDataSource.getRepository(StoryEntity);
  const journalRepo = AppDataSource.getRepository(JournalEntity);
  const messageRepo = AppDataSource.getRepository(MessageEntity);

  const storyIdMap = new Map<string, number>(); // old UUID -> new numeric ID
  const journalIdMap = new Map<string, number>(); // date/dailyChatId -> journal ID

  const storiesIndexPath = path.join(sourcePath, "stories-index.json");
  const storiesIndex = readJsonFile<OldStoriesIndex>(storiesIndexPath);

  if (!storiesIndex || !storiesIndex.stories.length) {
    console.log("   ⚠️ No stories found in stories-index.json");
    return { storyIdMap, journalIdMap };
  }

  stats.stories.total = storiesIndex.stories.length;

  for (const storyMeta of storiesIndex.stories) {
    try {
      // Check if story already exists by name
      const existing = await storyRepo.findOne({
        where: { name: storyMeta.name, userId: DEFAULT_USER_ID }
      });
      if (existing) {
        stats.stories.skipped++;
        storyIdMap.set(storyMeta.id, existing.id);
        continue;
      }

      // Read the story file to get storyPlot
      const storyFilePath = path.join(sourcePath, "stories", `${storyMeta.id}.json`);
      const storyFile = readJsonFile<OldStoryFile>(storyFilePath);

      const newStory = new StoryEntity();
      newStory.name = storyMeta.name;
      newStory.description = storyFile?.storyPlot || storyFile?.context || `Story created on ${storyMeta.createdAt}`;
      newStory.currentProgress = null;
      newStory.userId = DEFAULT_USER_ID;

      const savedStory = await storyRepo.save(newStory);
      storyIdMap.set(storyMeta.id, savedStory.id);
      stats.stories.success++;

      // Migrate journals from this story file
      if (storyFile && storyFile.journal && storyFile.journal.length > 0) {
        stats.journals.total += storyFile.journal.length;

        for (const oldJournal of storyFile.journal) {
          try {
            const identifier = oldJournal.id || oldJournal.date;

            // Check if journal already exists
            const existingJournal = await journalRepo
              .createQueryBuilder("j")
              .where("j.user_id = :userId", { userId: DEFAULT_USER_ID })
              .andWhere("j.story_id = :storyId", { storyId: savedStory.id })
              .andWhere("DATE(j.created_at) = :date", { date: oldJournal.date })
              .getOne();

            if (existingJournal) {
              stats.journals.skipped++;
              journalIdMap.set(identifier, existingJournal.id);
              journalIdMap.set(oldJournal.date, existingJournal.id);
              continue;
            }

            const newJournal = new JournalEntity();
            newJournal.summary = oldJournal.summary;
            newJournal.userId = DEFAULT_USER_ID;
            newJournal.storyId = savedStory.id;

            const savedJournal = await journalRepo.save(newJournal);
            journalIdMap.set(identifier, savedJournal.id);
            journalIdMap.set(oldJournal.date, savedJournal.id);
            if (oldJournal.id) {
              journalIdMap.set(oldJournal.id, savedJournal.id);
            }
            stats.journals.success++;

            // Migrate messages for this journal
            stats.messages.total += oldJournal.messages.length;
            for (const oldMsg of oldJournal.messages) {
              try {
                const existingMsg = await messageRepo.findOne({ where: { id: oldMsg.id } });
                if (existingMsg) {
                  stats.messages.skipped++;
                  continue;
                }

                // Migrate all messages - user messages get characterName = "User"
                const newMsg = new MessageEntity();
                newMsg.id = oldMsg.id;
                newMsg.content = oldMsg.text;
                newMsg.characterName = oldMsg.sender === "user" ? "User" : (oldMsg.characterName || "Unknown");
                newMsg.translation = oldMsg.translation ?? null;
                newMsg.tone = extractTone(oldMsg.rawText);
                newMsg.audio = oldMsg.audioData ?? null;
                newMsg.userId = DEFAULT_USER_ID;
                newMsg.journalId = savedJournal.id;

                await messageRepo.save(newMsg);
                stats.messages.success++;
              } catch (err) {
                const msg = `Message ${oldMsg.id}: ${err instanceof Error ? err.message : String(err)}`;
                stats.messages.errors.push(msg);
              }
            }
          } catch (err) {
            const msg = `Journal ${oldJournal.date}: ${err instanceof Error ? err.message : String(err)}`;
            stats.journals.errors.push(msg);
          }
        }
      }
    } catch (err) {
      const msg = `Story ${storyMeta.name}: ${err instanceof Error ? err.message : String(err)}`;
      stats.stories.errors.push(msg);
    }
  }

  console.log(`   ✅ Stories: ${stats.stories.success} migrated, ${stats.stories.skipped} skipped`);
  console.log(`   ✅ Journals: ${stats.journals.success} migrated, ${stats.journals.skipped} skipped`);
  console.log(`   ✅ Messages: ${stats.messages.success} migrated, ${stats.messages.skipped} skipped`);

  return { storyIdMap, journalIdMap };
}

// ============================================================================
// Main Migration Entry Point
// ============================================================================

/**
 * Prints migration summary report.
 */
function printReport(stats: MigrationStats): void {
  console.log("\n" + "=".repeat(60));
  console.log("📊 MIGRATION SUMMARY REPORT");
  console.log("=".repeat(60));

  const categories = [
    { name: "Stories", data: stats.stories },
    { name: "Vocabularies", data: stats.vocabularies },
    { name: "Vocabulary Reviews", data: stats.vocabularyReviews },
    { name: "Vocabulary Memories", data: stats.vocabularyMemories },
    { name: "Characters", data: stats.characters },
    { name: "Journals", data: stats.journals },
    { name: "Messages", data: stats.messages },
    { name: "Translation Cards", data: stats.translationCards },
    { name: "Translation Reviews", data: stats.translationReviews },
    { name: "Streak", data: stats.streak }
  ];

  for (const cat of categories) {
    const errorCount = cat.data.errors.length;
    const status = errorCount === 0 ? "✅" : "⚠️";
    console.log(
      `${status} ${cat.name.padEnd(20)} | Total: ${cat.data.total.toString().padStart(5)} | Success: ${cat.data.success.toString().padStart(5)} | Skipped: ${cat.data.skipped.toString().padStart(5)} | Errors: ${errorCount}`
    );
  }

  // Print error details
  let hasErrors = false;
  for (const cat of categories) {
    if (cat.data.errors.length > 0) {
      if (!hasErrors) {
        console.log("\n" + "=".repeat(60));
        console.log("❌ ERROR DETAILS");
        console.log("=".repeat(60));
        hasErrors = true;
      }
      console.log(`\n[${cat.name}]`);
      for (const err of cat.data.errors.slice(0, 10)) {
        console.log(`  - ${err}`);
      }
      if (cat.data.errors.length > 10) {
        console.log(`  ... and ${cat.data.errors.length - 10} more errors`);
      }
    }
  }

  console.log("\n" + "=".repeat(60));
}

/**
 * Main migration function.
 */
async function runMigration(sourcePath: string): Promise<void> {
  console.log("🚀 Starting migration from old MimiChat...");
  console.log(`📂 Source path: ${sourcePath}`);

  // Verify source path exists
  if (!fs.existsSync(sourcePath)) {
    console.error(`❌ Source path does not exist: ${sourcePath}`);
    process.exit(1);
  }

  // Initialize database
  console.log("\n🔌 Connecting to database...");
  await AppDataSource.initialize();
  console.log("   ✅ Database connected");

  // Ensure default user exists (ID=1) for migration
  console.log("\n👤 Checking default user...");
  await ensureDefaultUser();

  const stats = createEmptyStats();

  try {
    // Read source files
    const vocabStorePath = path.join(sourcePath, "vocabulary-store.json");
    const dataJsonPath = path.join(sourcePath, "data.json");
    const translationStorePath = path.join(sourcePath, "translation-store.json");
    const streakJsonPath = path.join(sourcePath, "streak.json");

    // 1. Migrate vocabularies
    const vocabStore = readJsonFile<OldVocabularyStore>(vocabStorePath);
    let vocabIdMap = new Map<string, string>();
    if (vocabStore) {
      vocabIdMap = await migrateVocabularies(vocabStore, stats);
      await migrateVocabularyReviews(vocabStore, vocabIdMap, stats);
      await migrateVocabularyMemories(vocabStore, vocabIdMap, stats);
    }

    // 2. Migrate stories (includes journals and messages from story files)
    const { journalIdMap } = await migrateStories(sourcePath, stats);

    // 3. Migrate characters from data.json
    const dataJson = readJsonFile<OldDataJson>(dataJsonPath);
    if (dataJson) {
      await migrateCharacters(dataJson, stats);
    }

    // 4. Migrate translation data
    const translationStore = readJsonFile<OldTranslationStore>(translationStorePath);
    if (translationStore) {
      await migrateTranslationData(translationStore, journalIdMap, stats);
    }

    // 5. Migrate streak
    const streakJson = readJsonFile<OldStreakJson>(streakJsonPath);
    if (streakJson) {
      await migrateStreak(streakJson, stats);
    }

    // Print report
    printReport(stats);

    console.log("\n✅ Migration completed!");
  } catch (err) {
    console.error("\n❌ Migration failed:", err);
    process.exit(1);
  } finally {
    await AppDataSource.destroy();
  }
}

// ============================================================================
// CLI Entry Point
// ============================================================================

const args = process.argv.slice(2);

/**
 * Parse source path from CLI arguments.
 * Supports:
 *   --source <path>
 *   <path> (positional argument)
 */
function parseSourcePath(): string | null {
  const sourceIndex = args.indexOf("--source");
  if (sourceIndex !== -1 && args[sourceIndex + 1]) {
    return args[sourceIndex + 1];
  }
  // Try positional argument (first non-flag arg)
  for (const arg of args) {
    if (!arg.startsWith("-")) {
      return arg;
    }
  }
  return null;
}

const sourcePath = parseSourcePath();

if (!sourcePath) {
  console.log("Usage:");
  console.log("  npm run migrate:old -- --source <path-to-old-server-data>");
  console.log("  npm run migrate:old -- <path-to-old-server-data>");
  console.log("");
  console.log("Example:");
  console.log("  npm run migrate:old -- D:/Unity/mimichat/server/data");
  process.exit(1);
}

runMigration(sourcePath);
