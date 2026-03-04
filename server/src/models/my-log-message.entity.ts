import { BeforeInsert, Column, CreateDateColumn, Entity, JoinColumn, ManyToOne, PrimaryColumn } from "typeorm";
import { v4 as uuidv4 } from "uuid";
import MyLogJournalEntity from "./my-log-journal.entity.js";
import UserEntity from "./user.entity.js";

/**
 * Persists individual chat messages from a diary chat session (MyLog feature).
 *
 * Messages with `isHidden = true` are psychologist evaluation entries that are
 * stored for AI context but never sent to the client.
 */
@Entity({ name: "my_log_messages" })
class MyLogMessageEntity {
  @PrimaryColumn({ type: "varchar", length: 36 })
  id!: string;

  @BeforeInsert()
  generateId(): void {
    if (!this.id) {
      this.id = uuidv4();
    }
  }

  /**
   * Message text content (Korean for characters, English for psychologist evaluations).
   */
  @Column({ type: "text" })
  content!: string;

  /**
   * Speaker name. "__PsychologistEval" for hidden clinical notes.
   */
  @Column({ name: "character_name", type: "varchar", length: 120 })
  characterName!: string;

  /**
   * Vietnamese translation of the message text. Empty for psychologist entries.
   */
  @Column({ name: "translation", type: "text", nullable: true })
  translation?: string | null;

  /**
   * Tone descriptor for TTS. "Clinical" for psychologist entries.
   */
  @Column({ name: "tone", type: "varchar", length: 120, nullable: true })
  tone?: string | null;

  /**
   * Audio file reference for TTS playback. Null for psychologist entries.
   */
  @Column({ name: "audio", type: "varchar", length: 255, nullable: true })
  audio?: string | null;

  /**
   * Whether this message is hidden from the client (psychologist evaluations).
   */
  @Column({ name: "is_hidden", type: "boolean", default: false })
  isHidden!: boolean;

  @Column({ name: "user_id", type: "int" })
  userId!: number;

  @Column({ name: "journal_id", type: "int" })
  journalId!: number;

  @ManyToOne(() => UserEntity, { nullable: false, onDelete: "CASCADE" })
  @JoinColumn({ name: "user_id" })
  user!: UserEntity;

  @ManyToOne(() => MyLogJournalEntity, { nullable: false, onDelete: "CASCADE" })
  @JoinColumn({ name: "journal_id" })
  journal!: MyLogJournalEntity;

  @CreateDateColumn({ name: "created_at", type: "datetime" })
  createdAt!: Date;
}

export default MyLogMessageEntity;
