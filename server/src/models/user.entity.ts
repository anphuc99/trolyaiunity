import {
  Entity,
  PrimaryGeneratedColumn,
  Column,
  CreateDateColumn,
  UpdateDateColumn
} from "typeorm";

/**
 * User entity for MimiLearn.
 *
 * Stores account credentials, profile information, reward points and money.
 */
@Entity({ name: "users" })
export default class UserEntity {
  @PrimaryGeneratedColumn("increment")
  id!: number;

  /** Unique login username. */
  @Column({ type: "varchar", length: 120, unique: true })
  username!: string;

  /** Bcrypt-hashed password. */
  @Column({ name: "password_hash", type: "varchar", length: 255 })
  passwordHash!: string;

  /** Display name. */
  @Column({ type: "varchar", length: 120, nullable: true })
  name?: string | null;

  /** User's age. */
  @Column({ type: "int", nullable: true })
  age?: number | null;

  /** Self-description / bio. */
  @Column({ type: "text", nullable: true })
  description?: string | null;

  /** Reward points earned through learning activities. */
  @Column({ name: "reward_points", type: "int", default: 0 })
  rewardPoints!: number;

  /** In-app currency. */
  @Column({ type: "int", default: 0 })
  money!: number;

  @CreateDateColumn({ name: "created_at", type: "datetime" })
  createdAt!: Date;

  @UpdateDateColumn({ name: "updated_at", type: "datetime" })
  updatedAt!: Date;
}
