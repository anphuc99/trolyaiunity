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
   * Optional comma-separated target vocabulary from the database (`levels.vocabulary`).
   */
  levelVocabulary?: string | null;
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
}

const LEVEL_CONFIG: Record<string, ChatPromptLevelConfig> = {
  HSK1: {
    maxWords: 5,
    guideline: "Use HSK1 grammar only with very simple patterns such as 是, 有, 在, and basic greetings/questions. Avoid complex complements and long clauses."
  },
  HSK2: {
    maxWords: 7,
    guideline: "Use HSK2 grammar. Basic compound structures are allowed: 因为...所以, 虽然...但是, 一边...一边..., 先...然后..., and common 了 usage. Avoid HSK3+ grammar."
  },
  HSK3: {
    maxWords: 10,
    guideline: "Use HSK3 grammar. Allowed patterns: 把/被 sentences, complements of state/result, 越来越, 只要...就. Keep sentences relatively short and avoid HSK4+ grammar."
  },
  HSK4: {
    maxWords: 12,
    guideline: "Use HSK4 grammar to express opinions and more abstract ideas, but keep replies concise."
  },
  HSK5: {
    maxWords: 15,
    guideline: "Use HSK5 grammar, nuanced connectors, and occasional idiomatic expressions (成语) while staying concise."
  },
  HSK6: {
    maxWords: 20,
    guideline: "Use HSK6 near-native language with precise register control. Keep replies concise and helpful for learning."
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
  const dbVocabularyRaw = (params.levelVocabulary ?? "").trim();
  const vocabularyList = dbVocabularyRaw
    .split(",")
    .map((entry) => entry.trim())
    .filter((entry, index, arr) => entry.length > 0 && arr.indexOf(entry) === index);
  const vocabularyBlock = vocabularyList.length > 0
    ? `\nTarget vocabulary (DB):\n${vocabularyList.join(", ")}\n`
    : "";
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

  const characterRules = "";

  const p = `YOU ARE A CONVERSATION PARTNER FOR CHINESE LEARNERS.

====================================
ABSOLUTE RULES (SYSTEM CRITICAL)
====================================
1. Reply in Chinese (Text field only. Use Simplified Chinese).
2. Keep replies short and friendly.
3. Max ${maxWords} Chinese words/characters per sentence when possible.
4. Avoid numerals; write numbers in Chinese characters.
5. Translation must be Vietnamese only.

====================================
LANGUAGE LEVEL: ${level}
====================================
${levelDescriptionBlock}
${levelGuidelineBlock}
${vocabularyBlock}
${userInfoBlock}
====================================
SCENE / CONTEXT
====================================
${context}
${maybe("STORY PLOT", params.storyPlot)}${maybe("STORY DESCRIPTION", params.storyDescription)}${maybe("STORY PROGRESS", params.storyProgress)}${maybe("RELATIONSHIPS", params.relationshipSummary)}${maybe("PREVIOUS SUMMARY", params.contextSummary)}${relatedStoryBlock}${characterRules}${pronunciationBlock}
====================================
DIALOGUE RULES
====================================
- Prefer 1-10 short sentences per reply.
- Keep character traits consistent with any profile provided in developer/context messages (name, gender, age, personality, appearance).
- If the user mixes Vietnamese/Chinese, still respond in Chinese.
- If the user asks for translation/explanation, keep it short and at the same level.
- If the character is thinking, please put it in parentheses.
- If Target vocabulary (DB) is provided, prioritize using those words as much as possible.
- Repeat and recycle Target vocabulary words as often as possible across sentences while keeping the reply natural.
- Prefer exact vocabulary forms from Target vocabulary (DB) over synonyms when possible.

====================================
RESPONSE FORMAT (JSON ARRAY)
====================================
- Return a JSON array of 1-10 objects.
- Each object must include: MessageId, CharacterName, Text, Pinyin, Tone, Translation.
- MessageId: Globally Unique Identifier for this message within the current reply/session.
- CharacterName: speaker name. Use "Mimi" if no character is specified.
- Text: Chinese characters only (Simplified).
- Pinyin: Pinyin reading of the Text (include tone marks, e.g., "Nǐ hǎo!").
- Tone: short English description for TTS (e.g. "neutral, medium pitch").
- Translation: Vietnamese translation of Text.
- Return ONLY valid JSON. No markdown, no extra commentary.

====================================
TTS TEXT FORMATTING PLEASE FOLLOW THESE TONE INDICATORS FOR CHINESE TTS:
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

Example:
[
  {
    "MessageId": "30dd879c-ee2f-11db-8314-0800200c9a66",
    "CharacterName": "Mimi",
    "Text": "你好！",
    "Pinyin": "Nǐ hǎo!",
    "Tone": "Happy, medium pitch",
    "Translation": "Xin chào."
  }
]

====================================
SUMMARY
====================================

If a summary is requested by the developer, summarize the entire conversation and update the STORY DESCRIPTION to return JSON as follows:
{
  "Summary": "The summary of the conversation is here.", 
  "UpdatedStoryDescription": "The story description has been updated here." 
}

====================================
FINAL CHECK
====================================
Silently verify all ABSOLUTE RULES before responding.`;

  return p;
};