import {
  Entity,
  PrimaryGeneratedColumn,
  Column,
  CreateDateColumn,
  ManyToOne,
  JoinColumn
} from "typeorm";
import UserEntity from "./user.entity.js";
import CharacterEntity from "./character.entity.js";

/**
 * Journal entity for MimiLearn.
 *
 * Represents a summarized conversation record. Created when user ends
 * a chat session with a character.
 */
@Entity({ name: "journals" })
export default class JournalEntity {
  @PrimaryGeneratedColumn("increment")
  id!: number;

  /** AI-generated conversation summary. */
  @Column({ type: "text" })
  summary!: string;

  @Column({ name: "character_id", type: "int", nullable: true })
  characterId?: number | null;

  @ManyToOne(() => CharacterEntity, { nullable: true, onDelete: "SET NULL" })
  @JoinColumn({ name: "character_id" })
  character?: CharacterEntity | null;

  @Column({ name: "user_id", type: "int" })
  userId!: number;

  @ManyToOne(() => UserEntity, { nullable: false, onDelete: "CASCADE" })
  @JoinColumn({ name: "user_id" })
  user!: UserEntity;

  @CreateDateColumn({ name: "created_at", type: "datetime" })
  createdAt!: Date;
}
