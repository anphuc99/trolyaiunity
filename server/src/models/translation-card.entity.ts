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
 * Stores a translation drill card built from a chat message.
 */
@Entity({ name: "translation_cards" })
@Index(["messageId", "userId"], { unique: true })
class TranslationCardEntity {
  @PrimaryGeneratedColumn("increment")
  id!: number;

  @Column({ name: "message_id", type: "varchar", length: 36 })
  messageId!: string;

  @Column({ type: "text" })
  content!: string;

  @Column({ type: "text", nullable: true })
  translation?: string | null;

  @Column({ name: "user_translation", type: "text", nullable: true })
  userTranslation?: string | null;

  @Column({ name: "character_name", type: "varchar", length: 120 })
  characterName!: string;

  @Column({ name: "audio", type: "varchar", length: 255, nullable: true })
  audio?: string | null;

  @Column({ name: "explanation_md", type: "text", nullable: true })
  explanationMd?: string | null;

  @Column({ name: "journal_id", type: "int" })
  journalId!: number;

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

export default TranslationCardEntity;
