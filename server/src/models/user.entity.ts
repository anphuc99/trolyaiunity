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

  /**
   * User's display name.
   */
  @Column({ type: "varchar", length: 120, nullable: true })
  name?: string | null;

  /**
   * User's age.
   */
  @Column({ type: "int", nullable: true })
  age?: number | null;

  /**
   * Self-description / bio.
   */
  @Column({ type: "text", nullable: true })
  description?: string | null;

  /**
   * TTS voice name (similar to character voice).
   */
  @Column({ type: "varchar", length: 64, nullable: true })
  voiceName?: string | null;

  /**
   * TTS voice pitch (similar to character pitch).
   */
  @Column({ type: "float", nullable: true })
  pitch?: number | null;

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
