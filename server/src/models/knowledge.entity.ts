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
import SubjectEntity from "./subject.entity.js";

/**
 * Knowledge entity for MimiLearn.
 *
 * Represents a single piece of knowledge within a subject.
 * Each knowledge item is linked to a subject and automatically gets
 * an FSRS review schedule upon creation.
 */
@Entity({ name: "knowledges" })
export default class KnowledgeEntity {
  @PrimaryGeneratedColumn("increment")
  id!: number;

  /** Knowledge item name / title. */
  @Column({ type: "varchar", length: 255 })
  name!: string;

  /** Optional description or content of the knowledge item. */
  @Column({ type: "text", nullable: true })
  description?: string | null;

  @Column({ name: "subject_id", type: "int" })
  subjectId!: number;

  @ManyToOne(() => SubjectEntity, { nullable: false, onDelete: "CASCADE" })
  @JoinColumn({ name: "subject_id" })
  subject!: SubjectEntity;

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
