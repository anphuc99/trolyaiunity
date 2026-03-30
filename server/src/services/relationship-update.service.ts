import type { DataSource, Repository } from "typeorm";
import CharacterRelationshipEntity from "../models/character-relationship.entity.js";
import CharacterEntity from "../models/character.entity.js";
import type { CheapAIService, RelationshipUpdateResult } from "./cheap-ai.service.js";
import type { VectorMemoryService } from "./vector-memory.service.js";
import type { ChatHistoryStore } from "./chat-history.service.js";

// ────────────────────────────────────────────────────────────────────────────
// Types
// ────────────────────────────────────────────────────────────────────────────

export interface RelationshipUpdateService {
  /**
   * Evaluates and updates relationships for all active characters after a
   * chat session ends. Runs the cheap AI pipeline:
   * 1. Summarize session impact
   * 2. For each character with relationships:
   *    a. Generate decision queries
   *    b. Query ChromaDB per query
   *    c. Evaluate relationship update
   *    d. Apply update to DB
   *
   * @param userId - Authenticated user id.
   * @param activeCharacterNames - Characters that were active during the session.
   * @param storyId - Current story id (for memory scoping), or null.
   */
  evaluateSession: (
    userId: number,
    activeCharacterNames: string[],
    storyId: number | null
  ) => Promise<void>;
}

export interface RelationshipUpdateServiceConfig {
  dataSource: DataSource;
  cheapAI: CheapAIService;
  vectorMemory: VectorMemoryService;
  historyStore: ChatHistoryStore;
}

// ────────────────────────────────────────────────────────────────────────────
// Factory
// ────────────────────────────────────────────────────────────────────────────

/**
 * Creates the relationship update service that orchestrates post-session
 * evaluation of character relationships through the cheap AI pipeline.
 *
 * @param config - Required dependencies.
 * @returns The relationship update service.
 */
export const createRelationshipUpdateService = (
  config: RelationshipUpdateServiceConfig
): RelationshipUpdateService => {
  const { dataSource, cheapAI, vectorMemory, historyStore } = config;
  const relationshipRepo: Repository<CharacterRelationshipEntity> =
    dataSource.getRepository(CharacterRelationshipEntity);
  const characterRepo: Repository<CharacterEntity> =
    dataSource.getRepository(CharacterEntity);

  const evaluateSession: RelationshipUpdateService["evaluateSession"] = async (
    userId,
    activeCharacterNames,
    storyId
  ) => {
    if (activeCharacterNames.length === 0) return;

    // Load chat history for session summary
    const history = await historyStore.load(userId);
    if (history.length === 0) return;

    const chatLines = history
      .filter((msg) => msg.role === "user" || msg.role === "assistant")
      .slice(-30) // Last 30 turns for evaluation
      .map((msg) => `${msg.role}: ${msg.content.slice(0, 300)}`);

    if (chatLines.length === 0) return;

    // Step 1: Summarize the session impact
    const sessionSummary = await cheapAI.summarizeSessionImpact(chatLines, activeCharacterNames);
    console.log("[RelationshipUpdate] Session summary:", sessionSummary);

    // Resolve character IDs from names
    const characters = await characterRepo
      .createQueryBuilder("c")
      .where("c.user_id = :userId", { userId })
      .andWhere("c.name IN (:...names)", { names: activeCharacterNames })
      .getMany();

    if (characters.length === 0) return;

    const characterIds = characters.map((c) => c.id);
    const nameById = new Map(characters.map((c) => [c.id, c.name]));
    const personalityById = new Map(characters.map((c) => [c.id, c.personality]));

    // Fetch all relationships for active characters
    const relationships = await relationshipRepo
      .createQueryBuilder("r")
      .where("r.ownerCharacterId IN (:...ids)", { ids: characterIds })
      .andWhere("r.userId = :userId", { userId })
      .getMany();

    if (relationships.length === 0) return;

    // Step 2-5: Process each relationship
    for (const rel of relationships) {
      try {
        const ownerName = nameById.get(rel.ownerCharacterId) ?? `Character#${rel.ownerCharacterId}`;
        const targetName =
          rel.targetType === "user"
            ? "User"
            : (nameById.get(rel.targetCharacterId ?? 0) ?? `Character#${rel.targetCharacterId}`);
        const personality = personalityById.get(rel.ownerCharacterId) ?? "";

        // Step 2: Generate decision queries
        const queries = await cheapAI.generateDecisionQueries(
          sessionSummary,
          ownerName,
          targetName,
          rel.relationshipKind,
          rel.stableThought,
          rel.stableEmotion
        );

        console.log(`[RelationshipUpdate] ${ownerName}→${targetName}: ${queries.length} queries generated`);

        // Step 3: Query ChromaDB for evidence
        const evidenceParts: string[] = [];
        for (const query of queries) {
          try {
            const results = await vectorMemory.query(userId, query, {
              storyId,
              topK: 2,
              actor: ownerName
            });

            for (const result of results) {
              evidenceParts.push(`- ${result.text}`);
            }

            // Also query global memories for broader context
            const globalResults = await vectorMemory.query(userId, query, {
              storyId,
              topK: 1,
              excludeActorMemories: true
            });

            for (const result of globalResults) {
              evidenceParts.push(`- [global] ${result.text}`);
            }
          } catch (queryError) {
            console.warn(`[RelationshipUpdate] Query failed for: "${query}"`, queryError);
          }
        }

        // Deduplicate evidence
        const uniqueEvidence = [...new Set(evidenceParts)];
        const retrievedMemories = uniqueEvidence.length > 0
          ? uniqueEvidence.join("\n")
          : "(No relevant memories found)";

        // Step 4: Evaluate relationship update
        const updateResult: RelationshipUpdateResult = await cheapAI.evaluateRelationshipUpdate({
          sessionSummary,
          retrievedMemories,
          ownerName,
          targetName,
          relationshipKind: rel.relationshipKind,
          stableThought: rel.stableThought,
          temporaryThought: rel.temporaryThought ?? null,
          stableEmotion: rel.stableEmotion,
          currentEmotion: rel.currentEmotion,
          emotionCause: rel.emotionCause ?? null,
          personality
        });

        console.log(
          `[RelationshipUpdate] ${ownerName}→${targetName}: delta=${updateResult.stableEmotionDelta}, rewrite=${updateResult.stableThoughtRewrite}, shock=${updateResult.isShockEvent}`
        );

        // Step 5: Apply update to DB
        rel.stableEmotion = Math.min(10, Math.max(0, rel.stableEmotion + updateResult.stableEmotionDelta));
        rel.currentEmotion = updateResult.newCurrentEmotion;
        rel.temporaryThought = updateResult.newTemporaryThought || null;
        rel.emotionCause = updateResult.emotionCause || null;

        if (updateResult.stableThoughtRewrite !== "none" && updateResult.newStableThought) {
          rel.stableThought = updateResult.newStableThought;
        }

        await relationshipRepo.save(rel);

        console.log(
          `[RelationshipUpdate] ${ownerName}→${targetName}: saved. stableEmotion=${rel.stableEmotion.toFixed(1)}, currentEmotion=${rel.currentEmotion.toFixed(1)}`
        );
      } catch (relError) {
        console.error(
          `[RelationshipUpdate] Failed to evaluate relationship id=${rel.id}:`,
          relError
        );
      }
    }
  };

  return { evaluateSession };
};
