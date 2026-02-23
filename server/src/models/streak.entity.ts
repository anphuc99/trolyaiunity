import {
  Column,
  CreateDateColumn,
  Entity,
  Index,
  JoinColumn,
  ManyToOne,
  PrimaryGeneratedColumn,
  UpdateDateColumn
} from "typeorm";
import UserEntity from "./user.entity.js";

/**
 * Persists the user learning streak, updated when daily tasks are completed.
 */
@Entity({ name: "streaks" })
@Index(["userId"], { unique: true })
class StreakEntity {
  @PrimaryGeneratedColumn("increment")
  id!: number;

  @Column({ name: "user_id", type: "int" })
  userId!: number;

  @ManyToOne(() => UserEntity, { nullable: false, onDelete: "CASCADE" })
  @JoinColumn({ name: "user_id" })
  user!: UserEntity;

  @Column({ name: "current_streak", type: "int", default: 0 })
  currentStreak!: number;

  @Column({ name: "longest_streak", type: "int", default: 0 })
  longestStreak!: number;

  @Column({ name: "last_completed_date", type: "datetime", nullable: true })
  lastCompletedDate!: Date | null;

  @CreateDateColumn({ name: "created_at", type: "datetime" })
  createdAt!: Date;

  @UpdateDateColumn({ name: "updated_at", type: "datetime" })
  updatedAt!: Date;
}

export default StreakEntity;
