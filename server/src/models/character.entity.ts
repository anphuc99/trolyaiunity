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

/**
 * Persists character profiles for the Characters view group.
 */
@Entity({ name: "characters" })
class CharacterEntity {
  @PrimaryGeneratedColumn("increment")
  id!: number;

  @Column({ type: "varchar", length: 120 })
  name!: string;

  @Column({ type: "text" })
  personality!: string;

  @Column({ type: "varchar", length: 12 })
  gender!: "male" | "female";

  @Column({ type: "text", nullable: true })
  appearance?: string | null;

  @Column({ type: "varchar", length: 512, nullable: true })
  avatar?: string | null;

  @Column({ type: "varchar", length: 32, nullable: true })
  voiceModel?: string | null;

  @Column({ type: "varchar", length: 64, nullable: true })
  voiceName?: string | null;

  @Column({ type: "float", nullable: true })
  pitch?: number | null;

  @Column({ type: "float", nullable: true })
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

export default CharacterEntity;
