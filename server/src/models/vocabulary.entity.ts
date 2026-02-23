import {
  Column,
  CreateDateColumn,
  Entity,
  JoinColumn,
  ManyToOne,
  PrimaryColumn,
  UpdateDateColumn,
  BeforeInsert
} from "typeorm";
import { v4 as uuidv4 } from "uuid";
import UserEntity from "./user.entity.js";

/**
 * Persists collected vocabulary items for spaced repetition review.
 * Uses string UUID for compatibility with old data migration.
 */
@Entity({ name: "vocabularies" })
class VocabularyEntity {
  @PrimaryColumn({ type: "varchar", length: 36 })
  id!: string;

  @BeforeInsert()
  generateId(): void {
    if (!this.id) {
      this.id = uuidv4();
    }
  }

  /** Korean word or phrase. */
  @Column({ type: "varchar", length: 255 })
  korean!: string;

  /** Vietnamese translation. */
  @Column({ type: "varchar", length: 255 })
  vietnamese!: string;

  /** Whether user added manually (not from chat). */
  @Column({ name: "is_manually_added", type: "boolean", default: false })
  isManuallyAdded!: boolean;

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

export default VocabularyEntity;
