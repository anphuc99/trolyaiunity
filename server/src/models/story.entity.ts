import { Column, CreateDateColumn, Entity, JoinColumn, ManyToOne, OneToMany, PrimaryGeneratedColumn, UpdateDateColumn } from "typeorm";
import JournalEntity from "./journal.entity.js";
import UserEntity from "./user.entity.js";

/**
 * Persists user-created stories that group multiple journals.
 */
@Entity({ name: "stories" })
class StoryEntity {
  @PrimaryGeneratedColumn("increment")
  id!: number;

  @Column({ type: "varchar", length: 160 })
  name!: string;

  @Column({ type: "text" })
  description!: string;

  @Column({ name: "current_progress", type: "text", nullable: true })
  currentProgress?: string | null;

  @Column({ name: "user_id", type: "int" })
  userId!: number;

  @ManyToOne(() => UserEntity, { nullable: false, onDelete: "CASCADE" })
  @JoinColumn({ name: "user_id" })
  user!: UserEntity;

  @OneToMany(() => JournalEntity, (journal) => journal.story)
  journals!: JournalEntity[];

  @CreateDateColumn({ name: "created_at", type: "datetime" })
  createdAt!: Date;

  @UpdateDateColumn({ name: "updated_at", type: "datetime" })
  updatedAt!: Date;
}

export default StoryEntity;
