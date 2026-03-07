import {
  Entity,
  PrimaryGeneratedColumn,
  Column,
  CreateDateColumn,
  UpdateDateColumn,
  ManyToOne,
  JoinColumn
} from "typeorm";
import UserEntity from "./user.entity.js";
import KnowledgeEntity from "./knowledge.entity.js";

/**
 * Knowledge review entity for MimiLearn.
 *
 * Stores FSRS (Free Spaced Repetition Scheduler) state for a knowledge item.
 * Each knowledge has at most one review row per user.
 */
@Entity({ name: "knowledge_reviews" })
export default class KnowledgeReviewEntity {
  @PrimaryGeneratedColumn("increment")
  id!: number;

  @Column({ name: "knowledge_id", type: "int" })
  knowledgeId!: number;

  @ManyToOne(() => KnowledgeEntity, { nullable: false, onDelete: "CASCADE" })
  @JoinColumn({ name: "knowledge_id" })
  knowledge!: KnowledgeEntity;

  @Column({ name: "user_id", type: "int" })
  userId!: number;

  @ManyToOne(() => UserEntity, { nullable: false, onDelete: "CASCADE" })
  @JoinColumn({ name: "user_id" })
  user!: UserEntity;

  /** FSRS stability — days until recall probability drops to desired retention. */
  @Column({ type: "float", default: 0 })
  stability!: number;

  /** FSRS difficulty [1–10]. Higher = harder to remember. */
  @Column({ type: "float", default: 5 })
  difficulty!: number;

  /** Number of times the item was rated "Again". */
  @Column({ type: "int", default: 0 })
  lapses!: number;

  /** Current scheduled interval in days. */
  @Column({ name: "current_interval_days", type: "int", default: 1 })
  currentIntervalDays!: number;

  /** Datetime of the next review. */
  @Column({ name: "next_review_date", type: "datetime" })
  nextReviewDate!: Date;

  /** Datetime of the last review (null if never reviewed). */
  @Column({ name: "last_review_date", type: "datetime", nullable: true })
  lastReviewDate!: Date | null;

  /**
   * Review history stored as JSON array.
   * Each entry: { date, rating, stabilityBefore, stabilityAfter,
   *   difficultyBefore, difficultyAfter, retrievability }
   */
  @Column({ name: "review_history", type: "text", default: "[]" })
  reviewHistoryJson!: string;

  @CreateDateColumn({ name: "created_at", type: "datetime" })
  createdAt!: Date;

  @UpdateDateColumn({ name: "updated_at", type: "datetime" })
  updatedAt!: Date;
}
