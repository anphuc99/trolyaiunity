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
 * Subject entity for MimiLearn.
 *
 * Represents a study subject created by a user. Each subject contains
 * multiple knowledge items.
 */
@Entity({ name: "subjects" })
export default class SubjectEntity {
  @PrimaryGeneratedColumn("increment")
  id!: number;

  /** Subject name. */
  @Column({ type: "varchar", length: 255 })
  name!: string;

  /** Optional description of the subject. */
  @Column({ type: "text", nullable: true })
  description?: string | null;

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
