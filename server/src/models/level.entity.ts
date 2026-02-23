import { Column, CreateDateColumn, Entity, PrimaryGeneratedColumn, UpdateDateColumn } from "typeorm";

/**
 * Persists CEFR proficiency level metadata.
 */
@Entity({ name: "levels" })
class LevelEntity {
  @PrimaryGeneratedColumn("increment")
  id!: number;

  @Column({ type: "varchar", length: 8, unique: true })
  level!: string;

  @Column({ name: "max_words", type: "int", default: 5 })
  maxWords!: number;

  @Column({ type: "text", default: "" })
  guideline!: string;

  @Column({ type: "text" })
  descript!: string;

  @CreateDateColumn({ name: "created_at", type: "datetime" })
  createdAt!: Date;

  @UpdateDateColumn({ name: "updated_at", type: "datetime" })
  updatedAt!: Date;
}

export default LevelEntity;
