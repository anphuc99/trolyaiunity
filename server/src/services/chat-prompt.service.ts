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
   * Optional long-term memory brief retrieved from ChromaDB.
   * Injected by the memory retrieval service before building the prompt.
   */
  longTermMemoryBrief?: string | null;
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

  const characterRules = "";

  const longTermMemoryBrief = (params.longTermMemoryBrief ?? "").trim();
  const longTermMemoryBlock = longTermMemoryBrief
    ? `\n====================================\nLONG-TERM MEMORY (from previous conversations)\n====================================\n${longTermMemoryBrief}\nUse this memory naturally in your responses. Do not mention that you "retrieved" or "looked up" this information.\n`
    : "";

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
${userInfoBlock}
====================================
SCENE / CONTEXT
====================================
${context}
${maybe("STORY PLOT", params.storyPlot)}${maybe("STORY DESCRIPTION", params.storyDescription)}${maybe("STORY PROGRESS", params.storyProgress)}${maybe("RELATIONSHIPS", params.relationshipSummary)}${maybe("PREVIOUS SUMMARY", params.contextSummary)}${relatedStoryBlock}${characterRules}${pronunciationBlock}${longTermMemoryBlock}
====================================
DIALOGUE RULES
====================================
- Prefer 1-10 short sentences per reply.
- Keep character traits consistent with any profile provided in developer/context messages (name, gender, age, personality, appearance).
- If the user mixes Vietnamese/Chinese, still respond in Chinese.
- If the user asks for translation/explanation, keep it short and at the same level.
- If the character is thinking, please put it in parentheses.

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
- OPTIONAL MEMORY EXTRACTION: You MAY include memory sidecar fields on assistant reply items when a truly important long-term fact emerges.
  There are TWO types of memory:

  A) GLOBAL MEMORY (objective facts about the world/story):
     - Allowed ONLY on the FIRST item in the array.
     - Uses prefix "Global": "GlobalMemoryEn", "GlobalMemoryType", "GlobalMemoryImportance".
     - Do NOT include "ImportantMemoryActor".
     - Write GlobalMemoryEn in third-person objective voice.
     - Example: "The group decided to visit the park this weekend."

  B) CHARACTER MEMORY (subjective thoughts/feelings/preferences of a specific character):
     - Allowed on ANY item in the array, attached to the item whose CharacterName owns the memory.
     - Uses prefix "Important": "ImportantMemoryEn", "ImportantMemoryType", "ImportantMemoryImportance", "ImportantMemoryActor".
     - MUST include "ImportantMemoryActor" matching that item's CharacterName.
     - Write ImportantMemoryEn in FIRST-PERSON from that character's perspective.
     - Example: ImportantMemoryActor: "Mimi", ImportantMemoryEn: "I love fried chicken the most."
     - Multiple characters can each have their own memory in the same reply.
     - Two characters can also store memories about the same event from their own perspective.
     - The FIRST item can have BOTH a GlobalMemory* and an ImportantMemory* at the same time (different field names).

  Required sidecar fields:
  - Global memory: "GlobalMemoryEn" (third-person, 1 sentence), "GlobalMemoryType", "GlobalMemoryImportance".
  - Character memory: "ImportantMemoryEn" (first-person, 1 sentence), "ImportantMemoryType", "ImportantMemoryImportance", "ImportantMemoryActor".
  - Type values: "preference", "relationship", "story_fact", "plan", "profile", "learning".
  - Importance values: "high" or "medium".

  IMPORTANT: Do NOT emit memories for every reply. Only store truly important, lasting facts worth remembering across sessions:
  food preferences, relationship changes, story-critical events, future plans, recurring learning mistakes.
  Do NOT emit for greetings, filler, momentary emotions, or trivial small talk.
  Avoid memory inflation — if unsure whether something is important enough, do NOT emit.
  If nothing important happened, do NOT include these fields.
  Memory text MUST be in English regardless of conversation language.

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

Example (normal reply — no important memory):
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

Example (GLOBAL memory on first item — objective fact, no actor):
[
  {
    "MessageId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "CharacterName": "Mimi",
    "Text": "好的，我们这周末去公园！",
    "Pinyin": "Hǎo de, wǒmen zhè zhōumò qù gōngyuán!",
    "Tone": "Happy, medium pitch",
    "Translation": "Được rồi, chúng ta sẽ đi công viên cuối tuần này!",
    "ImportantMemoryEn": "The group decided to visit the park this weekend.",
    "ImportantMemoryType": "plan",
    "ImportantMemoryImportance": "high"
  }
]

Example (CHARACTER memory on first item — subjective first-person voice):
[
  {
    "MessageId": "b0c1d2e3-f4a5-6789-abcd-ef1234567890",
    "CharacterName": "Mimi",
    "Text": "那我们去吃炸鸡吧！",
    "Pinyin": "Nà wǒmen qù chī zhá jī ba!",
    "Tone": "Happy, medium pitch",
    "Translation": "Vậy chúng ta đi ăn gà rán nhé!",
    "ImportantMemoryEn": "I love fried chicken the most.",
    "ImportantMemoryType": "preference",
    "ImportantMemoryImportance": "high",
    "ImportantMemoryActor": "Mimi"
  }
]

Example (two characters each storing their own memory):
[
  {
    "MessageId": "b1c2d3e4-f5a6-7890-abcd-111111111111",
    "CharacterName": "Mimi",
    "Text": "我喜欢吃炸鸡！",
    "Pinyin": "Wǒ xǐhuan chī zhá jī!",
    "Tone": "Happy, medium pitch",
    "Translation": "Tôi thích ăn gà rán!",
    "ImportantMemoryEn": "I love fried chicken the most.",
    "ImportantMemoryType": "preference",
    "ImportantMemoryImportance": "high",
    "ImportantMemoryActor": "Mimi"
  },
  {
    "MessageId": "c2d3e4f5-a6b7-8901-bcde-222222222222",
    "CharacterName": "Lisa",
    "Text": "我更喜欢披萨！",
    "Pinyin": "Wǒ gèng xǐhuan pīsà!",
    "Tone": "Happy, medium pitch",
    "Translation": "Tôi thích pizza hơn!",
    "ImportantMemoryEn": "I prefer pizza over other food.",
    "ImportantMemoryType": "preference",
    "ImportantMemoryImportance": "high",
    "ImportantMemoryActor": "Lisa"
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