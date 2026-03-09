import {
  Entity,
  PrimaryGeneratedColumn,
  Column,
  CreateDateColumn,
  UpdateDateColumn,
  ManyToOne,
  JoinColumn
} from "typeorm";
import UserEntity from "./user.entity.js";

/**
 * Character entity for MimiLearn.
 *
 * Represents an AI conversation partner with a name, personality description,
 * avatar image, and voice configuration.
 */
@Entity({ name: "characters" })
export default class CharacterEntity {
  @PrimaryGeneratedColumn("increment")
  id!: number;

  /** Character display name. */
  @Column({ type: "varchar", length: 120 })
  name!: string;

  /** Character personality / description. */
  @Column({ type: "text" })
  personality!: string;

  /** Character gender (male / female). */
  @Column({ type: "varchar", length: 12 })
  gender!: "male" | "female";

  /** Character's age. */
  @Column({ type: "int", nullable: true })
  age?: number | null;

  /** Character appearance description. */
  @Column({ type: "text", nullable: true })
  appearance?: string | null;

  /** Avatar image URL or path. */
  @Column({ type: "varchar", length: 512, nullable: true })
  avatar?: string | null;

  /** TTS voice model (e.g. "openai"). */
  @Column({ name: "voice_model", type: "varchar", length: 32, nullable: true })
  voiceModel?: string | null;

  /** TTS voice name. */
  @Column({ name: "voice_name", type: "varchar", length: 64, nullable: true })
  voiceName?: string | null;

  /** TTS voice pitch multiplier. */
  @Column({ type: "float", nullable: true })
  pitch?: number | null;

  /** TTS speaking rate multiplier. */
  @Column({ name: "speaking_rate", type: "float", nullable: true })
  speakingRate?: number | null;

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
