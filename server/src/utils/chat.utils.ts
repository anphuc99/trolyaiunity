import { randomUUID } from "crypto";
import type { ChatHistoryMessage } from "../services/chat-history.service.js";
import type MessageEntity from "../models/message.entity.js";
import { buildAudioId } from "../services/tts.service.js";

export interface AssistantTurn {
  MessageId?: string;
  CharacterName?: string;
  Text?: string;
  Pinyin?: string;
  Tone?: string;
  Translation?: string;
  Transcribe?: string;
}

let hasLoggedAssistantReplyParseFailure = false;

export const parseAssistantReply = (content: string): AssistantTurn[] => {
  const trimmed = content.trim();

  if (!trimmed) {
    return [];
  }

  const tryParse = (input: string) => {
    try {
      const parsed = JSON.parse(input) as unknown;
      if (Array.isArray(parsed)) {
        return parsed as AssistantTurn[];
      }
      if (parsed && typeof parsed === "object") {
        return [parsed as AssistantTurn];
      }
    } catch (error) {
      if (!hasLoggedAssistantReplyParseFailure) {
        console.warn("Failed to parse assistant reply as JSON; attempting fallback extraction.", error);
        hasLoggedAssistantReplyParseFailure = true;
      }
      return null;
    }

    return null;
  };

  const direct = tryParse(trimmed);
  if (direct) {
    return direct;
  }

  const arrayStart = trimmed.indexOf("[");
  const arrayEnd = trimmed.lastIndexOf("]");
  if (arrayStart !== -1 && arrayEnd > arrayStart) {
    const sliced = tryParse(trimmed.slice(arrayStart, arrayEnd + 1));
    if (sliced) {
      return sliced;
    }
  }

  const objectStart = trimmed.indexOf("{");
  const objectEnd = trimmed.lastIndexOf("}");
  if (objectStart !== -1 && objectEnd > objectStart) {
    const sliced = tryParse(trimmed.slice(objectStart, objectEnd + 1));
    if (sliced) {
      return sliced;
    }
  }

  return [];
};

export const parseAssistantEditNote = (content: string) => {
  const englishMatch = content.match(/^Assistant\s+message\s+edited:\s+([^\.\n]+)\./i);
  const vietnameseMatch = content.match(/^Chat\s+co\s+messageID\s+duoc\s+sua\s+thanh\s+([^\.\n]+)\./i);
  const idMatch = englishMatch ?? vietnameseMatch;

  if (!idMatch) {
    return null;
  }

  const messageId = idMatch[1].trim();
  if (!messageId) {
    return null;
  }

  const englishContentMatch = content.match(/New\s+content:\s*([\s\S]+)/i);
  const vietnameseContentMatch = content.match(/Noi\s+dung\s+moi:\s*([\s\S]+)/i);
  const contentMatch = englishContentMatch ?? vietnameseContentMatch;
  const updatedText = contentMatch ? contentMatch[1].trim() : "";
  if (!updatedText) {
    return null;
  }

  return { messageId, updatedText };
};

export const applyAssistantEdits = <T extends { role: string; content: string }>(history: T[]): T[] => {
  const edits = new Map<string, string>();

  for (const message of history) {
    if (message.role !== "developer") {
      continue;
    }

    const edit = parseAssistantEditNote(message.content);
    if (edit) {
      edits.set(edit.messageId, edit.updatedText);
    }
  }

  if (!edits.size) {
    return history;
  }

  return history.map((message) => {
    if (message.role !== "assistant") {
      return message;
    }

    const turns = parseAssistantReply(message.content);
    if (!turns.length) {
      return message;
    }

    let didUpdate = false;
    const nextTurns = turns.map((turn) => {
      const turnId = typeof turn.MessageId === "string" ? turn.MessageId.trim() : "";
      const updatedText = turnId ? edits.get(turnId) : null;

      if (updatedText) {
        didUpdate = true;
        return { ...turn, Text: updatedText };
      }

      return turn;
    });

    if (!didUpdate) {
      return message;
    }

    return {
      ...message,
      content: JSON.stringify(nextTurns)
    };
  });
};

export const parseDeveloperCharacterAction = (content: string) => {
  const addedMatch = content.match(/^Character\s+"([^"]+)"\s+has been added\./m);
  if (addedMatch) {
    return { name: addedMatch[1].trim(), active: true };
  }

  const removedMatch = content.match(/^Character\s+"([^"]+)"\s+has been removed from this conversation\./m);
  if (removedMatch) {
    return { name: removedMatch[1].trim(), active: false };
  }

  return null;
};

export const parseDeveloperLearningPathState = (content: string) => {
  if (!/^Developer\s+learning\s+path\s+applied:/i.test(content)) {
    return null;
  }

  const idMatch = content.match(/^LearningPathId:\s*(\d+)\s*$/im);
  const contextMatch = content.match(/^LearningPathContext:\s*\n([\s\S]*?)\nLearningPathVocabulary:\s*$/im);
  const vocabMatch = content.match(/^LearningPathVocabulary:\s*\n([\s\S]*?)(?:\nAI requirements:|$)/im);

  const id = idMatch ? Number.parseInt(idMatch[1], 10) : Number.NaN;
  const context = contextMatch?.[1]?.trim() ?? "";
  const vocabulary = vocabMatch?.[1]?.trim() ?? "";

  if (!Number.isInteger(id) || id <= 0 || !context || !vocabulary) {
    return null;
  }

  return {
    learningPathId: id,
    context,
    vocabulary
  };
};

export const normalizeName = (value: string) => value.trim().toLowerCase();

export const buildMessageEntities = (
  history: ChatHistoryMessage[],
  userId: number,
  journalId: number | null,
  voiceByCharacter: Map<string, { voiceModel: string; voiceName: string; pitch: number | null; speakingRate: number | null }>
): Array<Pick<MessageEntity, "content" | "characterName" | "translation" | "pinyin" | "tone" | "audio" | "userId" | "journalId">> => {
  const result: Array<Pick<MessageEntity, "content" | "characterName" | "translation" | "pinyin" | "tone" | "audio" | "userId" | "journalId">> = [];

  for (const message of history) {
    if (message.role === "user") {
      const content = message.content.trim();
      if (!content) {
        continue;
      }

      result.push({
        content,
        characterName: "User",
        translation: null,
        pinyin: null,
        tone: null,
        audio: null,
        userId,
        journalId
      });
      continue;
    }

    if (message.role !== "assistant") {
      continue;
    }

    const turns = parseAssistantReply(message.content);
    if (!turns.length) {
      const fallback = message.content.trim();
      if (!fallback) {
        continue;
      }

      result.push({
        content: fallback,
        characterName: "Mimi",
        translation: null,
        pinyin: null,
        tone: null,
        audio: null,
        userId,
        journalId
      });
      continue;
    }

    for (const turn of turns) {
      const content = typeof turn.Text === "string" ? turn.Text.trim() : "";
      if (!content) {
        continue;
      }

      const characterName = typeof turn.CharacterName === "string" ? turn.CharacterName.trim() : "Mimi";
      const translation = typeof turn.Translation === "string" ? turn.Translation.trim() : "";
      const pinyin = typeof turn.Pinyin === "string" ? turn.Pinyin.trim() : "";
      const tone = typeof turn.Tone === "string" ? turn.Tone.trim() : "";
      const voiceKey = normalizeName(characterName || "Mimi");
      const voiceSettings = voiceByCharacter.get(voiceKey);
      const audio = tone ? buildAudioId(content, tone, `${voiceSettings?.voiceModel ?? "openai"}:${voiceSettings?.voiceName ?? ""}`, voiceSettings?.pitch ?? undefined, voiceSettings?.speakingRate ?? undefined) : null;

      result.push({
        content,
        characterName: characterName || "Mimi",
        translation: translation || null,
        pinyin: pinyin || null,
        tone: tone || null,
        audio,
        userId,
        journalId
      });
    }
  }

  return result;
};
