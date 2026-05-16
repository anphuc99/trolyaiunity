import {
  Index,
  Column,
  CreateDateColumn,
  Entity,
  JoinColumn,
  ManyToOne,
  PrimaryColumn,
  UpdateDateColumn,
  BeforeInsert
} from "typeorm";
import { v4 as uuidv4 } from "uuid";
import UserEntity from "./user.entity.js";

/**
 * Persists collected vocabulary items together with their FSRS spaced-repetition
 * review state. Review fields are nullable so that pre-existing vocabulary rows
 * (created before the tables were merged) are still valid.
 *
 * Uses string UUID for compatibility with old data migration.
 */
@Index(["userId", "chinnese"], { unique: true })
@Entity({ name: "vocabularies" })
class VocabularyEntity {
  @PrimaryColumn({ type: "varchar", length: 36 })
  id!: string;

  @BeforeInsert()
  generateId(): void {
    if (!this.id) {
      this.id = uuidv4();
    }
  }

  /** Chinese word or phrase. */
  @Column({ type: "varchar", length: 255 })
  chinnese!: string;

  /** Vietnamese translation. */
  @Column({ type: "varchar", length: 255 })
  vietnamese!: string;

  /** Optional pinyin reading. */
  @Column({ type: "varchar", length: 255, nullable: true })
  pinyin?: string | null;

  /** Optional vocabulary level label (for example: HSK1, HSK2, Beginner). */
  @Column({ type: "varchar", length: 100, nullable: true })
  level?: string | null;

  /** Whether user added manually (not from chat). */
  @Column({ name: "is_manually_added", type: "boolean", default: false })
  isManuallyAdded!: boolean;

  /** Whether this vocabulary is ignored and excluded from practice reviews. */
  @Column({ name: "is_ignored", type: "boolean", default: false })
  isIgnored!: boolean;

  // ── FSRS review state ─────────────────────────────────────────────────────
  // All fields nullable: rows created before the merge have no review data yet.

  /** FSRS stability — days until recall probability drops to desired retention. */
  @Column({ type: "float", nullable: true, default: null })
  stability?: number | null;

  /** FSRS difficulty [1–10]. Higher = harder to remember. */
  @Column({ type: "float", nullable: true, default: null })
  difficulty?: number | null;

  /** Number of times the card was rated "Again". */
  @Column({ type: "int", nullable: true, default: null })
  lapses?: number | null;

  /** Current scheduled interval in days. */
  @Column({ name: "current_interval_days", type: "int", nullable: true, default: null })
  currentIntervalDays?: number | null;

  /** ISO date of the next review (null = review state not yet initialised). */
  @Column({ name: "next_review_date", type: "datetime", nullable: true, default: null })
  nextReviewDate?: Date | null;

  /** ISO date of the last review (null if never reviewed). */
  @Column({ name: "last_review_date", type: "datetime", nullable: true, default: null })
  lastReviewDate?: Date | null;

  /** Card direction preference: 'kr-vn' or 'vn-kr'. */
  @Column({ name: "card_direction", type: "varchar", length: 12, nullable: true, default: "kr-vn" })
  cardDirection?: string | null;

  /** User can star/favourite a word. */
  @Column({ name: "is_starred", type: "boolean", nullable: true, default: false })
  isStarred?: boolean | null;

  /**
   * Review history stored as a JSON array.
   * Each entry: { date, rating, stabilityBefore, stabilityAfter,
   *   difficultyBefore, difficultyAfter, retrievability }
   */
  @Column({ name: "review_history", type: "text", nullable: true, default: null })
  reviewHistoryJson?: string | null;

  // ─────────────────────────────────────────────────────────────────────────

  @Column({ name: "user_id", type: "int" })
  userId!: number;

  @ManyToOne(() => UserEntity, { nullable: false, onDelete: "CASCADE" })
  @JoinColumn({ name: "user_id" })
  user!: UserEntity;

  @CreateDateColumn({ name: "created_at", type: "datetime" })
  createdAt!: Date;

  @UpdateDateColumn({ name: "updated_at", type: "datetime" })
  updatedAt!: Date;
}

export default VocabularyEntity;
