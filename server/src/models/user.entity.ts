import {
  Column,
  CreateDateColumn,
  Entity,
  JoinColumn,
  ManyToOne,
  PrimaryGeneratedColumn,
  UpdateDateColumn
} from "typeorm";
import LevelEntity from "./level.entity.js";
import StoryEntity from "./story.entity.js";

/**
 * Persists application users for authentication.
 */
@Entity({ name: "users" })
class UserEntity {
  @PrimaryGeneratedColumn("increment")
  id!: number;

  @Column({ type: "varchar", length: 120, unique: true })
  username!: string;

  @Column({ name: "password_hash", type: "varchar", length: 255 })
  passwordHash!: string;

  @Column({ name: "level_id", type: "int", nullable: true })
  levelId?: number | null;

  @ManyToOne(() => LevelEntity, { nullable: true, onDelete: "SET NULL" })
  @JoinColumn({ name: "level_id" })
  level?: LevelEntity | null;

  /**
   * The currently active story for this user.
   * Used as fallback when API requests don't specify a storyId.
   */
  @Column({ name: "current_story_id", type: "int", nullable: true })
  currentStoryId?: number | null;

  @ManyToOne(() => StoryEntity, { nullable: true, onDelete: "SET NULL" })
  @JoinColumn({ name: "current_story_id" })
  currentStory?: StoryEntity | null;

  @CreateDateColumn({ name: "created_at", type: "datetime" })
  createdAt!: Date;

  @UpdateDateColumn({ name: "updated_at", type: "datetime" })
  updatedAt!: Date;
}

export default UserEntity;
