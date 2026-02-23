export interface ChatPromptLevelConfig {
  maxWords: number;
  guideline: string;
}

export interface ChatPromptParams {
  /**
   * CEFR level label (A0-C2). Unknown levels fall back to A1.
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
  A0: {
    maxWords: 3,
    guideline: "Use only simple present tense. Avoid any complex grammar."
  },
  A1: {
    maxWords: 5,
    guideline: "Use simple sentences. Present tense and basic past. Allowed patterns: -고 싶다, -아/어요."
  },
  A2: {
    maxWords: 7,
    guideline: "Basic A2 compound structures are allowed: -고, -지만, -아서/-어서, -(으)면, -(으)려고. Avoid intermediate-level grammar."
  },
  B1: {
    maxWords: 10,
    guideline:
      "Use lower-intermediate (B1) grammar. Keep sentences not too long. Allowed patterns: -(으)ㄹ 수 있다, -아/어서, -(으)니까, -기 때문에, -(으)면, -는데, -(으)려고 하다, -(으)면서, -(으)ㄴ/는 것 같다, -아/어도 되다, -아/어야 하다. Avoid B2+ grammar."
  },
  B2: {
    maxWords: 12,
    guideline: "Use advanced grammar. Express opinions and more abstract ideas, but keep replies concise."
  },
  C1: {
    maxWords: 15,
    guideline: "Use advanced grammar, idiomatic expressions, and nuanced language while staying concise."
  },
  C2: {
    maxWords: 20,
    guideline: "Use natural, native-like language. Keep replies concise and helpful for learning."
  }
};

const normalizeLevel = (value: string | null | undefined) => {
  const trimmed = (value ?? "").trim().toUpperCase();
  return trimmed && trimmed in LEVEL_CONFIG ? trimmed : "A1";
};

/**
 * Builds a system instruction string inspired by the legacy `initChat()` prompt.
 *
 * Important: this project expects JSON array assistant replies (legacy Gemini-style).
 * So we keep the same *rules + context structure*, but we enforce the JSON schema in the prompt.
 *
 * Any optional field that is missing/empty is skipped ("cái nào chưa có bỏ qua").
 */
export const buildChatSystemPrompt = (params: ChatPromptParams): string => {
  const level = normalizeLevel(params.level);
  const levelCfg = LEVEL_CONFIG[level];
  const dbMaxWords = typeof params.levelMaxWords === "number" ? params.levelMaxWords : null;
  const maxWords = Number.isFinite(dbMaxWords) && (dbMaxWords as number) > 0 ? (dbMaxWords as number) : levelCfg.maxWords;
  const dbGuideline = (params.levelGuideline ?? "").trim();
  const guideline = dbGuideline || levelCfg.guideline;
  const context = params.context?.trim() ? params.context.trim() : "A casual Korean practice chat between the user and the assistant.";

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

  const relatedStoryMessages = (params.relatedStoryMessages ?? "").trim();
  const relatedStoryBlock = relatedStoryMessages
    ? `\n====================================\nRELATED STORY REFERENCE\n====================================\nThe following transcript is from related story episodes. Use it as context reference only.\n\n${relatedStoryMessages}\n`
    : "";

  const pronunciationBlock = params.checkPronunciation
    ? `\n====================================\nPRONUNCIATION CHECK\n====================================\n- If the user asks for pronunciation help, reply with a short correction and a short example sentence at the current level.\n- Ask one short clarifying question if the user intent is unclear.\n`
    : "";

  const characterRules = "";

  const p = `YOU ARE A CONVERSATION PARTNER FOR KOREAN LEARNERS.

====================================
ABSOLUTE RULES (SYSTEM CRITICAL)
====================================
1. Reply in Korean (Text field only).
2. Keep replies short and friendly.
3. Max ${maxWords} Korean words per sentence when possible.
4. Avoid numerals; write numbers in Korean words.
5. Translation must be Vietnamese only.

====================================
LANGUAGE LEVEL: ${level}
====================================
${levelDescriptionBlock}
${levelGuidelineBlock}

====================================
SCENE / CONTEXT
====================================
${context}
${maybe("STORY PLOT", params.storyPlot)}${maybe("STORY DESCRIPTION", params.storyDescription)}${maybe("STORY PROGRESS", params.storyProgress)}${maybe("RELATIONSHIPS", params.relationshipSummary)}${maybe("PREVIOUS SUMMARY", params.contextSummary)}${relatedStoryBlock}${characterRules}${pronunciationBlock}
====================================
DIALOGUE RULES
====================================
- Prefer 1-10 short sentences per reply.
- If the user mixes Vietnamese/Korean, still respond in Korean.
- If the user asks for translation/explanation, keep it short and at the same level.

====================================
RESPONSE FORMAT (JSON ARRAY)
====================================
- Return a JSON array of 1-10 objects.
- Each object must include: MessageId, CharacterName, Text, Tone, Translation.
- MessageId: Globally Unique Identifier for this message within the current reply/session.
- CharacterName: speaker name. Use "Mimi" if no character is specified.
- Text: Korean only.
- Tone: short English description for TTS (e.g. "neutral, medium pitch").
- Translation: Vietnamese translation of Text.
- Return ONLY valid JSON. No markdown, no extra commentary.

====================================
TTS TEXT FORMATTING PlEASE FOLLOW THESE TONE INDICATORS FOR KOREAN TTS:
====================================
Angry: !!!
Shouting: !!!!!
Disgusted: 응... ...  
Sad: ... ...  
Scared: 아... ...  
Surprised: 흥?! ?!  
Shy: ...  
Affectionate: 흥...  
Happy: !  
Excited: 와! !!!  
Serious: .  
Neutral: unchanged

Example:
[
  {
    "MessageId": "30dd879c-ee2f-11db-8314-0800200c9a66", -- Use a UUID v1/v4 generator
    "CharacterName": "Mimi",
    "Text": "안녕하세요!",
    "Tone": "Happy, medium pitch",
    "Translation": "Xin chao."
  }
]

====================================
SUMMARY
====================================

If a summary is requested by the developer, summarize the entire conversation and update the STORY DESCRIPTION to return JSON as follows:
{
  "Summary": "The summary of the conversation is here.", -- Only Vietnamese summary text.
  "UpdatedStoryDescription": "The story description has been updated here." -- Only Vietnamese updated story description text.
}

====================================
FINAL CHECK
====================================
Silently verify all ABSOLUTE RULES before responding.`;

  return p;
};

