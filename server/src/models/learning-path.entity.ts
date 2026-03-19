import { Column, CreateDateColumn, Entity, JoinColumn, ManyToOne, PrimaryGeneratedColumn, UpdateDateColumn } from "typeorm";
import UserEntity from "./user.entity.js";

/**
 * Persists user-designed learning paths.
 * Each path stores a linear story context and comma-separated vocabulary.
 */
@Entity({ name: "learning_paths" })
class LearningPathEntity {
  @PrimaryGeneratedColumn("increment")
  id!: number;

  @Column({ type: "text" })
  context!: string;

  @Column({ type: "text" })
  vocabulary!: string;

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

export default LearningPathEntity;
