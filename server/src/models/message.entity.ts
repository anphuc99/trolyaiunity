import { Column, CreateDateColumn, Entity, JoinColumn, ManyToOne, PrimaryColumn, BeforeInsert } from "typeorm";
import { v4 as uuidv4 } from "uuid";
import JournalEntity from "./journal.entity.js";
import UserEntity from "./user.entity.js";

/**
 * Persists chat messages associated with a journal entry.
 * Uses string UUID for compatibility with old data migration.
 */
@Entity({ name: "messages" })
class MessageEntity {
  @PrimaryColumn({ type: "varchar", length: 36 })
  id!: string;

  @BeforeInsert()
  generateId(): void {
    if (!this.id) {
      this.id = uuidv4();
    }
  }

  @Column({ type: "text" })
  content!: string;

  @Column({ name: "character_name", type: "varchar", length: 120 })
  characterName!: string;

  @Column({ name: "translation", type: "text", nullable: true })
  translation?: string | null;

  @Column({ name: "tone", type: "varchar", length: 120, nullable: true })
  tone?: string | null;

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

export default MessageEntity;
