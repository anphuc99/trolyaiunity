import { Column, CreateDateColumn, Entity, Index, PrimaryGeneratedColumn, UpdateDateColumn } from "typeorm";

export type VoiceModel = "openai" | "gemini";

/**
 * Persists available TTS voices by provider model.
 */
@Entity({ name: "voices" })
@Index(["model", "voice"], { unique: true })
class VoiceEntity {
  @PrimaryGeneratedColumn("increment")
  id!: number;

  @Column({ type: "varchar", length: 32 })
  model!: VoiceModel;

  @Column({ type: "varchar", length: 64 })
  voice!: string;

  @CreateDateColumn({ name: "created_at", type: "datetime" })
  createdAt!: Date;

  @UpdateDateColumn({ name: "updated_at", type: "datetime" })
  updatedAt!: Date;
}

export default VoiceEntity;