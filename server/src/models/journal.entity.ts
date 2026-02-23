import { Column, CreateDateColumn, Entity, JoinColumn, ManyToOne, PrimaryGeneratedColumn } from "typeorm";
import StoryEntity from "./story.entity.js";
import UserEntity from "./user.entity.js";

/**
 * Persists summarized conversations (journals) for a user.
 */
@Entity({ name: "journals" })
class JournalEntity {
  @PrimaryGeneratedColumn("increment")
  id!: number;

  @Column({ type: "text" })
  summary!: string;

  @Column({ name: "user_id", type: "int" })
  userId!: number;

  @Column({ name: "story_id", type: "int", nullable: true })
  storyId?: number | null;

  @ManyToOne(() => UserEntity, { nullable: false, onDelete: "CASCADE" })
  @JoinColumn({ name: "user_id" })
  user!: UserEntity;

  @ManyToOne(() => StoryEntity, { nullable: true, onDelete: "SET NULL" })
  @JoinColumn({ name: "story_id" })
  story?: StoryEntity | null;

  @CreateDateColumn({ name: "created_at", type: "datetime" })
  createdAt!: Date;
}

export default JournalEntity;
