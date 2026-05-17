export interface ChatPromptLevelConfig {
  maxWords: number;
  guideline: string;
}

export interface ChatPromptParams {
  /**
  * HSK level label (HSK1-HSK6). Legacy A0-C2 values are mapped for backward compatibility.
   */
  level?: string | null;
  /**
    * Optional human-readable level description coming from the database.
    * Note: this is stored in the `levels.descript` column.
   */
    levelDescription?: string | null;
    /**
    * Optional grammar guideline coming from the database.
    * Note: this is stored in the `levels.guideline` column.
    */
    levelGuideline?: string | null;
    /**
    * Optional per-level max word limit coming from the database.
    */
    levelMaxWords?: number | null;
  /**
   * User's display name.
   */
  userName?: string | null;
  /**
   * User's age.
   */
  userAge?: number | null;
  /**
   * User's self-description or bio.
   */
  userDescription?: string | null;
  /**
   * Optional scene/context description. If missing, we default to a generic chat setting.
   */
  context?: string | null;
  /**
   * Optional plot reminder.
   */
  storyPlot?: string | null;
  /**
   * Optional story description provided by the user.
   */
  storyDescription?: string | null;
  /**
   * Optional current story progress provided by the user or AI.
   */
  storyProgress?: string | null;
  /**
   * Optional relationship summary.
   */
  relationshipSummary?: string | null;
  /**
   * Optional summary of the last journal entry to provide more context.
   */
  lastJournalSummary?: string | null;
  /**
   * Optional prior summary.
   */
  contextSummary?: string | null;
  /**
   * Optional related story transcript (previous episodes).
   */
  relatedStoryMessages?: string | null;
  /**
   * Optional pronunciation-check mode. If true, ask the user short clarifying questions.
   */
  checkPronunciation?: boolean;
  /**
   * Whether the RECALL_MEMORY command is available (ChromaDB + memory services configured).
   */
  memoryRecallEnabled?: boolean;
  /**
   * Names of characters currently active in the chat scene.
   * Used to constrain AI CharacterName output to valid characters only.
   */
  activeCharacterNames?: string[] | null;
}

const LEVEL_CONFIG: Record<string, ChatPromptLevelConfig> = {
  HSK1: {
    maxWords: 5,
    guideline: "Use HSK 3.0 Level 1 grammar only. Keep sentences extremely short and concrete. Prioritize basic sentence types and core patterns: simple affirmative/negative/interrogative sentences; A是B; 有/没有; 在 + location; this/that/which; basic possession with 的; basic modal verbs such as 想, 会, 能; simple adjective predicates with 很/太; simple verb-object statements; basic time expressions; simple imperative/polite forms with 请. Avoid comparison, serial clauses, complements, passive/disposal structures, abstract connectors, and any idiomatic compression."
  },
  HSK2: {
    maxWords: 7,
    guideline: "Use HSK 3.0 Level 2 grammar. Allow common daily-life expansion beyond Level 1: sentence-final 了 for change/new situation, experiential 过 in simple contexts, progressive 在/正在, simple result/state complements such as 到/见/好, basic comparison with 比, existence/location refinements, choice and sequence markers such as 还是, 或者, 先...再/然后..., frequency/time expressions, and common cause/result patterns in simple form. Use everyday topic sentences only. Avoid 把/被, complex complement chains, concessive structures with heavy subordination, and abstract argumentative writing."
  },
  HSK3: {
    maxWords: 10,
    guideline: "Use HSK 3.0 Level 3 grammar. Allow fuller everyday narration and explanation with intermediate patterns: directional complements, potential complements in common forms, comparison extensions, 连动/兼语 constructions, 越来越..., 一边...一边..., 一...就..., 先...然后..., 因为...所以..., 虽然...但是..., 如果...就..., 除了...以外..., 只要...就..., even basic 把/被 in very clear contexts. Use simple paragraph logic but keep each sentence concise. Avoid dense formal written style, heavy nominalization, advanced rhetorical inversion, and idioms unless extremely common."
  },
  HSK4: {
    maxWords: 12,
    guideline: "Use HSK 3.0 Level 4 grammar. Permit broader discussion, explanation, and opinion with clearer logical structure: 不但/不仅...而且..., 既...又..., 先...再..., 无论...都..., 即使...也..., 与其...不如..., 一方面...另一方面..., as well as more flexible 把/被, complement structures, and topic-comment organization. Sentences may be moderately complex but should remain readable and conversational. Avoid highly literary compression, overly formal bureaucratic phrasing, and HSK5-6 style abstract discourse density."
  },
  HSK5: {
    maxWords: 15,
    guideline: "Use HSK 3.0 Level 5 grammar. Allow mature discussion, explanation, and argument with richer connectors and nuanced stance marking: 之所以...是因为..., 并非...而是..., 不论/无论..., 既然..., 尽管..., 甚至..., 反而..., 从而..., 以便..., 以免..., 何况..., 况且..., rather complete complement usage, and occasional natural 成语 or书面词 when helpful. Keep logic explicit and elegant, but do not become excessively literary or obscure."
  },
  HSK6: {
    maxWords: 20,
    guideline: "Use HSK 3.0 Level 6 grammar with near-native flexibility and precise register control. Allow layered subordination, nuanced discourse markers, formal/informal register shifts, compact but natural argumentation, rhetorical emphasis, advanced complements, and idiomatic expressions when contextually appropriate. Maintain coherence, precision, and pedagogical readability. Avoid archaic classical wording unless explicitly requested."
  }
};

const LEGACY_LEVEL_MAP: Record<string, string> = {
  A0: "HSK1",
  A1: "HSK1",
  A2: "HSK2",
  B1: "HSK3",
  B2: "HSK4",
  C1: "HSK5",
  C2: "HSK6"
};

const normalizeLevel = (value: string | null | undefined) => {
  const trimmed = (value ?? "").trim().toUpperCase();
  if (trimmed && trimmed in LEVEL_CONFIG) {
    return trimmed;
  }

  if (trimmed && trimmed in LEGACY_LEVEL_MAP) {
    return LEGACY_LEVEL_MAP[trimmed];
  }

  return "HSK1";
};

/**
 * Builds a system instruction string inspired by the legacy `initChat()` prompt.
 *
 * Important: this project expects JSON array assistant replies (legacy Gemini-style).
 * So we keep the same *rules + context structure*, but we enforce the JSON schema in the prompt.
 *
 * Any optional field that is missing/empty is skipped.
 */
export const buildChatSystemPrompt = (params: ChatPromptParams): string => {
  const level = normalizeLevel(params.level);
  const levelCfg = LEVEL_CONFIG[level];
  const dbMaxWords = typeof params.levelMaxWords === "number" ? params.levelMaxWords : null;
  const maxWords = Number.isFinite(dbMaxWords) && (dbMaxWords as number) > 0 ? (dbMaxWords as number) : levelCfg.maxWords;
  const dbGuideline = (params.levelGuideline ?? "").trim();
  const guideline = dbGuideline || levelCfg.guideline;
  const context = params.context?.trim() ? params.context.trim() : "A casual Chinese practice chat between the user and the assistant.";

  const maybe = (label: string, value: string | null | undefined) => {
    const trimmed = (value ?? "").trim();
    if (!trimmed) {
      return "";
    }
    return `\n${label}:\n${trimmed}\n`;
  };

  const dbDescription = (params.levelDescription ?? "").trim();
  const levelDescriptionBlock = dbDescription ? `\nLevel description (DB):\n${dbDescription}\n` : "";
  const levelGuidelineBlock = dbGuideline ? `\nLevel guideline (DB):\n${dbGuideline}\n` : "";

  // Build user info block
  const userInfoParts: string[] = [];
  if (params.userName?.trim()) userInfoParts.push(`Name: ${params.userName.trim()}`);
  if (typeof params.userAge === "number" && params.userAge > 0) userInfoParts.push(`Age: ${params.userAge}`);
  if (params.userDescription?.trim()) userInfoParts.push(`Description: ${params.userDescription.trim()}`);
  const userInfoBlock = userInfoParts.length > 0
    ? `\n====================================\nUSER PROFILE\n====================================\n${userInfoParts.join("\n")}\n`
    : "";

  const relatedStoryMessages = (params.relatedStoryMessages ?? "").trim();
  const relatedStoryBlock = relatedStoryMessages
    ? `\n====================================\nRELATED STORY REFERENCE\n====================================\nThe following transcript is from related story episodes. Use it as context reference only.\n\n${relatedStoryMessages}\n`
    : "";

  const pronunciationBlock = params.checkPronunciation
    ? `\n====================================\nPRONUNCIATION CHECK\n====================================\n- If the user asks for pronunciation help, reply with a short correction and a short example sentence at the current level.\n- Ask one short clarifying question if the user intent is unclear.\n`
    : "";

  const activeNames = (params.activeCharacterNames ?? []).filter(n => n.trim());
  const characterRules = activeNames.length > 0
    ? `\n====================================\nACTIVE CHARACTERS IN SCENE\n====================================\nOnly the following characters are currently in the scene: ${activeNames.map(n => `"${n}"`).join(", ")}.\n- You MUST only use CharacterName values from this list.\n- Do NOT invent, fabricate, or use any character name that is not listed above.\n- If the list has only one character, all replies must use that character's name.\n`
    : "";

  const recallMemoryBlock = params.memoryRecallEnabled
    ? `\n====================================\nMEMORY RECALL COMMAND\n====================================\nYou have access to a long-term memory database from previous conversations.\nWhen you need specific information from past interactions to answer the user properly\n(e.g., their preferences, past events, character backstory details, promises, plans),\nyou can request a memory recall.\n\nTo recall memories, respond with ONLY this JSON format (instead of the normal JSON array):\n{"recall_memory": ["question 1 in English", "question 2 in English", ...]}\n\nRules:\n- Only use when you genuinely need past information that is NOT in the current conversation context.\n- Do NOT use for greetings, simple questions, or when the answer is obvious from context.\n- Write 2-6 questions in English, each 5-15 words.\n- Questions should target specific information you need.\n- After the system provides memory recall results (as a developer message), respond normally using the standard JSON array format.\n- Never mention the recall process to the user. Use recalled information naturally.\n- You may only use recall_memory once per conversation turn.\n\nExample:\nUser says: "我们上次说好要去哪里来着？"\nYou respond: {"recall_memory": ["previous trip plans discussed with user", "promises about outings or activities", "recent locations mentioned in conversations"]}\n`
    : "";

  const relationshipBlock = params.relationshipSummary?.trim()
    ? `\n====================================\nCHARACTER RELATIONSHIPS & EMOTIONS\n====================================\n${params.relationshipSummary.trim()}\n\nINSTRUCTIONS FOR RELATIONSHIPS:\n- Each character MUST behave consistently with their relationship kind, thoughts, and emotion levels.\n- "Stable emotion" reflects the deep, long-term bond (hard to change). "Current emotion" reflects the right-now feeling (volatile).\n- If currentEmotion is low but stableEmotion is high, the character is upset but still deeply bonded — they may act cold or hurt, but underlying affection remains.\n- If currentEmotion is high but stableEmotion is low, the character is momentarily pleased but still guarded or distant.\n- "Stable thought" is the character's core belief about the target. "Temporary thought" is a situational reaction that may override behavior temporarily.\n- Do NOT reveal these numbers or mechanics to the user. Express emotions through dialogue tone, word choice, and actions naturally.\n`
    : "";

  const p = `YOU ARE A CONVERSATION PARTNER FOR CHINESE LEARNERS.

====================================
ABSOLUTE RULES (SYSTEM CRITICAL)
====================================
1. Reply in Chinese (Hanzi field only. Use Simplified Chinese).
2. Keep replies short and friendly.
3. Max ${maxWords} Chinese words/characters per sentence when possible.
4. Avoid numerals; write numbers in Chinese characters.
5. Translation must be Vietnamese only.

====================================
LANGUAGE LEVEL: ${level}
====================================
${levelDescriptionBlock}
${levelGuidelineBlock}
${userInfoBlock}
====================================
SCENE / CONTEXT
====================================
${context}
${maybe("STORY PLOT", params.storyPlot)}${maybe("STORY DESCRIPTION", params.storyDescription)}${maybe("STORY PROGRESS", params.storyProgress)}${maybe("LAST JOURNAL SUMMARY", params.lastJournalSummary)}${relationshipBlock}${maybe("PREVIOUS SUMMARY", params.contextSummary)}${relatedStoryBlock}${characterRules}${pronunciationBlock}${recallMemoryBlock}
====================================
NARRATOR (叙述者)
====================================
- There is a special narrator character named "叙述者".
- "叙述者" describes character actions, scene changes, and atmosphere IN CHINESE (Simplified).
- "叙述者" MUST appear BEFORE each character's dialogue line to describe what the character is doing.
- "叙述者" always uses Emotion=neutral, Intensity=low.
- "叙述者" lines should be short descriptive narration in Chinese, NOT dialogue.
- "叙述者" Pinyin and Translation follow the same rules as normal characters.

====================================
DIALOGUE RULES
====================================
- Prefer 1-10 short sentences per reply.
- Keep character traits consistent with any profile provided in developer/context messages (name, gender, age, personality, appearance).
- If the user mixes Vietnamese/Chinese, still respond in Chinese.
- If the user asks for translation/explanation, keep it short and at the same level.
- If the character is thinking, please put it in parentheses.

====================================
RESPONSE FORMAT (PIPE-DELIMITED TEXT)
====================================
Each line is ONE message. Fields are separated by "|" (pipe character).
Field order: MessageId|CharacterName|Hanzi|Pinyin|Emotion|Intensity|Translation

Field definitions:
- MessageId: UUID string (e.g., "30dd879c-ee2f-11db-8314-0800200c9a66").
- CharacterName: speaker name OR "叙述者" for narration.
- Hanzi: Chinese text (Simplified). May contain Latin letters for foreign names.
- Pinyin: Pinyin reading of Hanzi. MUST SEPARATE EVERY SINGLE SYLLABLE WITH A SPACE (e.g., "Nǐ hǎo", "wǒ men" not "wǒmen"). Must NOT contain any Chinese characters.
- Emotion: MUST be exactly one of these 12 values (lowercase): angry, shouting, disgusted, sad, scared, surprised, shy, affectionate, happy, excited, serious, neutral.
- Intensity: MUST be exactly one of: low, medium, high.
- Translation: Vietnamese translation of Hanzi.

CRITICAL RULES:
- Return ONLY pipe-delimited lines. No JSON, no markdown, no extra commentary.
- Do NOT use "|" inside any field value.
- Each line must have exactly 7 fields separated by 6 pipe characters.
- "叙述者" narrator line MUST appear before each character dialogue line.

====================================
HANZI STYLE MARKERS
====================================
Angry: !!!
Shouting: !!!!!
Disgusted: 呃... ...
Sad: ... ...
Scared: 啊... ...
Surprised: 咦?! ?!
Shy: ...
Affectionate: 嗯...
Happy: !
Excited: 哇! !!!
Serious: .
Neutral: unchanged

====================================
EXAMPLES
====================================

Example (normal reply with narrator):
11111111-2222-3333-4444-555555555555|叙述者|Mimi 微笑着向你挥手。|Mimi wēi xiào zhe xiàng nǐ huī shǒu.|neutral|low|Mimi mỉm cười vẫy tay chào bạn.
30dd879c-ee2f-11db-8314-0800200c9a66|Mimi|你好！|Nǐ hǎo!|happy|low|Xin chào!

Example (angry scene):
a1b2c3d4-0000-1111-2222-333333333333|叙述者|Mimi 猛地站起来，瞪着你。|Mimi měng de zhàn qǐ lái, dèng zhe nǐ.|neutral|low|Mimi đột ngột đứng dậy, trừng mắt nhìn bạn.
a1b2c3d4-e5f6-7890-abcd-ef1234567890|Mimi|你怎么这样!!!|Nǐ zěn me zhè yàng!!!|angry|high|Sao bạn lại như vậy!

Example (two characters):
b0c1d2e3-0000-0000-0000-000000000001|叙述者|Mimi 开心地拍手。|Mimi kāi xīn de pāi shǒu.|neutral|low|Mimi vui vẻ vỗ tay.
b0c1d2e3-f4a5-6789-abcd-ef1234567890|Mimi|我喜欢吃炸鸡！|Wǒ xǐ huan chī zhá jī!|happy|high|Tôi thích ăn gà rán!
c2d3e4f5-0000-0000-0000-000000000002|叙述者|Lisa 笑着摇摇头。|Lisa xiào zhe yáo yáo tóu.|neutral|low|Lisa cười lắc đầu.
c2d3e4f5-a6b7-8901-bcde-222222222222|Lisa|我更喜欢披萨！|Wǒ gèng xǐ huan pī sà!|happy|medium|Tôi thích pizza hơn!

====================================
FINAL CHECK
====================================
Silently verify all ABSOLUTE RULES before responding.`;

  return p;
};