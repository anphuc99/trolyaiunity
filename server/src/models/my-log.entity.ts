import { Column, CreateDateColumn, Entity, JoinColumn, ManyToOne, PrimaryGeneratedColumn } from "typeorm";
import UserEntity from "./user.entity.js";

/**
 * Persists a user's daily diary/log entry.
 *
 * Each record stores the raw diary text, a spaced-repetition review schedule
 * (fixed intervals up to ~10 years), and an archive flag for completed reviews.
 */
@Entity({ name: "my_logs" })
class MyLogEntity {
  @PrimaryGeneratedColumn("increment")
  id!: number;

  /**
   * The diary text written by the user.
   */
  @Column({ type: "text" })
  content!: string;

  @Column({ name: "user_id", type: "int" })
  userId!: number;

  @ManyToOne(() => UserEntity, { nullable: false, onDelete: "CASCADE" })
  @JoinColumn({ name: "user_id" })
  user!: UserEntity;

  /**
   * Next scheduled review date. Null when all reviews are complete.
   */
  @Column({ name: "next_review_date", type: "datetime", nullable: true })
  nextReviewDate!: Date | null;

  /**
   * Number of times this entry has been reviewed (0–20).
   */
  @Column({ name: "review_count", type: "int", default: 0 })
  reviewCount!: number;

  /**
   * Whether all scheduled reviews have been completed.
   */
  @Column({ name: "is_archived", type: "boolean", default: false })
  isArchived!: boolean;

  @CreateDateColumn({ name: "created_at", type: "datetime" })
  createdAt!: Date;
}

export default MyLogEntity;
