import {
  Column,
  CreateDateColumn,
  Entity,
  JoinColumn,
  ManyToOne,
  PrimaryGeneratedColumn,
  Unique,
  UpdateDateColumn
} from "typeorm";
import CharacterEntity from "./character.entity.js";
import UserEntity from "./user.entity.js";

/**
 * Target type for a character relationship.
 * "user" means the relationship is toward the authenticated user.
 * "character" means the relationship is toward another character.
 */
export type RelationshipTargetType = "user" | "character";

/**
 * Persists structured relationship data between a character and a target
 * (either the user or another character). Each record represents the
 * one-directional psychological state of the owner character toward the target.
 */
@Entity({ name: "character_relationships" })
@Unique(["ownerCharacterId", "targetType", "targetCharacterId", "userId"])
class CharacterRelationshipEntity {
  @PrimaryGeneratedColumn("increment")
  id!: number;

  /**
   * The character who holds this relationship / feeling.
   */
  @Column({ name: "owner_character_id", type: "int" })
  ownerCharacterId!: number;

  @ManyToOne(() => CharacterEntity, { nullable: false, onDelete: "CASCADE" })
  @JoinColumn({ name: "owner_character_id" })
  ownerCharacter!: CharacterEntity;

  /**
   * Whether the target is the user or another character.
   */
  @Column({ name: "target_type", type: "varchar", length: 12 })
  targetType!: RelationshipTargetType;

  /**
   * When targetType is "character", the id of the target character.
   * Null when targetType is "user".
   */
  @Column({ name: "target_character_id", type: "int", nullable: true })
  targetCharacterId?: number | null;

  @ManyToOne(() => CharacterEntity, { nullable: true, onDelete: "CASCADE" })
  @JoinColumn({ name: "target_character_id" })
  targetCharacter?: CharacterEntity | null;

  /**
   * Social label for the relationship, e.g. "younger_sister", "friend",
   * "rival", "lover", "colleague", "stranger".
   */
  @Column({ name: "relationship_kind", type: "varchar", length: 60 })
  relationshipKind!: string;

  /**
   * Core first-person belief the owner holds about the target.
   * Hard to change — only updated at session end by cheap AI when
   * stable emotion shifts enough.
   */
  @Column({ name: "stable_thought", type: "text" })
  stableThought!: string;

  /**
   * Situational first-person reaction. Changes quickly with events.
   */
  @Column({ name: "temporary_thought", type: "text", nullable: true })
  temporaryThought?: string | null;

  /**
   * Long-term emotional bond intensity (0.0 – 10.0).
   * Moves slowly; only a shock event or sustained pattern shifts it fast.
   */
  @Column({ name: "stable_emotion", type: "float", default: 5.0 })
  stableEmotion!: number;

  /**
   * Current emotional intensity (0.0 – 10.0).
   * Highly volatile — can spike or crash within a single chat session.
   */
  @Column({ name: "current_emotion", type: "float", default: 5.0 })
  currentEmotion!: number;

  /**
   * Human-readable reason for the current emotion value.
   */
  @Column({ name: "emotion_cause", type: "text", nullable: true })
  emotionCause?: string | null;

  /**
   * Owner of this data (the authenticated user).
   */
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

export default CharacterRelationshipEntity;
