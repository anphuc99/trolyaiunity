import {
  Column,
  CreateDateColumn,
  Entity,
  JoinColumn,
  ManyToOne,
  PrimaryGeneratedColumn,
  UpdateDateColumn
} from "typeorm";
import UserEntity from "./user.entity.js";
import VocabularyEntity from "./vocabulary.entity.js";

/**
 * Persists FSRS spaced-repetition review state for a vocabulary item.
 */
@Entity({ name: "vocabulary_reviews" })
class VocabularyReviewEntity {
  @PrimaryGeneratedColumn("increment")
  id!: number;

  @Column({ name: "vocabulary_id", type: "varchar", length: 36, unique: true })
  vocabularyId!: string;

  @ManyToOne(() => VocabularyEntity, { nullable: false, onDelete: "CASCADE" })
  @JoinColumn({ name: "vocabulary_id" })
  vocabulary!: VocabularyEntity;

  /** FSRS stability — days until recall probability drops to desired retention. */
  @Column({ type: "float", default: 0 })
  stability!: number;

  /** FSRS difficulty [1–10]. Higher = harder to remember. */
  @Column({ type: "float", default: 5 })
  difficulty!: number;

  /** Number of times the card was rated "Again". */
  @Column({ type: "int", default: 0 })
  lapses!: number;

  /** Current scheduled interval in days. */
  @Column({ name: "current_interval_days", type: "int", default: 0 })
  currentIntervalDays!: number;

  /** ISO date of the next review. */
  @Column({ name: "next_review_date", type: "datetime" })
  nextReviewDate!: Date;

  /** ISO date of the last review (null if never reviewed). */
  @Column({ name: "last_review_date", type: "datetime", nullable: true })
  lastReviewDate?: Date | null;

  /** Card direction preference: 'kr-vn' or 'vn-kr'. */
  @Column({ name: "card_direction", type: "varchar", length: 12, default: "kr-vn" })
  cardDirection!: string;

  /** User can star/favourite a word. */
  @Column({ name: "is_starred", type: "boolean", default: false })
  isStarred!: boolean;

  /**
   * Review history stored as JSON array.
   * Each entry: { date, rating, stabilityBefore, stabilityAfter,
   *   difficultyBefore, difficultyAfter, retrievability }
   */
  @Column({ name: "review_history", type: "text", default: "[]" })
  reviewHistoryJson!: string;

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

export default VocabularyReviewEntity;
