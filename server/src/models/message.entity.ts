import {
  Entity,
  PrimaryColumn,
  Column,
  CreateDateColumn,
  ManyToOne,
  JoinColumn,
  BeforeInsert
} from "typeorm";
import { v4 as uuidv4 } from "uuid";
import UserEntity from "./user.entity.js";
import JournalEntity from "./journal.entity.js";

/**
 * Message entity for MimiLearn.
 *
 * Stores individual chat messages within a journal (conversation session).
 * Uses UUID primary key for client-side ID generation compatibility.
 */
@Entity({ name: "messages" })
export default class MessageEntity {
  @PrimaryColumn({ type: "varchar", length: 36 })
  id!: string;

  @BeforeInsert()
  generateId(): void {
    if (!this.id) {
      this.id = uuidv4();
    }
  }

  /** Message content text. */
  @Column({ type: "text" })
  content!: string;

  /** Name of the character who sent this message (or "user"). */
  @Column({ name: "character_name", type: "varchar", length: 120 })
  characterName!: string;

  /** Optional translation of the message content. */
  @Column({ name: "translation", type: "text", nullable: true })
  translation?: string | null;

  /** Tone / emotion of the message. */
  @Column({ name: "tone", type: "varchar", length: 120, nullable: true })
  tone?: string | null;

  /** Audio file URL or identifier. */
  @Column({ name: "audio", type: "varchar", length: 255, nullable: true })
  audio?: string | null;

  @Column({ name: "user_id", type: "int" })
  userId!: number;

  @Column({ name: "journal_id", type: "int" })
  journalId!: number;

  @ManyToOne(() => UserEntity, { nullable: false, onDelete: "CASCADE" })
  @JoinColumn({ name: "user_id" })
  user!: UserEntity;

  @ManyToOne(() => JournalEntity, { nullable: false, onDelete: "CASCADE" })
  @JoinColumn({ name: "journal_id" })
  journal!: JournalEntity;

  @CreateDateColumn({ name: "created_at", type: "datetime" })
  createdAt!: Date;
}
