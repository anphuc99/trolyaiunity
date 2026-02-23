import {
  Column,
  CreateDateColumn,
  Entity,
  JoinColumn,
  ManyToOne,
  PrimaryGeneratedColumn,
  UpdateDateColumn
} from "typeorm";
import UserEntity from "./user.entity.js";
import VocabularyEntity from "./vocabulary.entity.js";

/**
 * Persists a user's memory/note associated with a vocabulary item.
 * Memory content can include text, linked message references [MSG:id],
 * and embedded images [IMG:url].
 */
@Entity({ name: "vocabulary_memories" })
class VocabularyMemoryEntity {
  @PrimaryGeneratedColumn("increment")
  id!: number;

  @Column({ name: "vocabulary_id", type: "varchar", length: 36, unique: true })
  vocabularyId!: string;

  @ManyToOne(() => VocabularyEntity, { nullable: false, onDelete: "CASCADE" })
  @JoinColumn({ name: "vocabulary_id" })
  vocabulary!: VocabularyEntity;

  /** Serialized memory content (text + [MSG:id] + [IMG:url] markers). */
  @Column({ name: "user_memory", type: "text" })
  userMemory!: string;

  /** JSON array of message IDs linked inside the memory. */
  @Column({ name: "linked_message_ids", type: "text", default: "[]" })
  linkedMessageIdsJson!: string;

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

export default VocabularyMemoryEntity;
