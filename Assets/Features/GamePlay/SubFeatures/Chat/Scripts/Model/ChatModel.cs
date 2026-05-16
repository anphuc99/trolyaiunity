using Newtonsoft.Json;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Chat.Model
{
	/// <summary>
	/// Payload for loading chat history from server.
	/// </summary>
	public sealed class ChatHistoryRequestPayload
	{
		/// <summary>
		/// Optional session id for segmented history streams.
		/// </summary>
		[JsonProperty("sessionId")]
		public string SessionId { get; set; }
	}

	/// <summary>
	/// Payload for sending a chat message to server.
	/// </summary>
	public sealed class ChatSendRequestPayload
	{
		/// <summary>
		/// User message content.
		/// </summary>
		[JsonProperty("message")]
		public string Message { get; set; }

		/// <summary>
		/// Optional session id for segmented history streams.
		/// </summary>
		[JsonProperty("sessionId")]
		public string SessionId { get; set; }

		/// <summary>
		/// Optional model override.
		/// </summary>
		[JsonProperty("model")]
		public string Model { get; set; }

		/// <summary>
		/// Optional base64-encoded audio data URL for voice messages.
		/// When present, the server sends the audio to Gemini directly
		/// and returns a transcription in the response.
		/// </summary>
		[JsonProperty("audio")]
		public string Audio { get; set; }

		/// <summary>
		/// Client-side only: message id of the user bubble so the view can
		/// update it with the transcribed text after the server responds.
		/// Not serialized to the server.
		/// </summary>
		[JsonIgnore]
		public string AudioMessageId { get; set; }
	}

	/// <summary>
	/// Payload from view to request replaying message audio.
	/// </summary>
	public sealed class ChatPlayMessageAudioRequestPayload
	{
		/// <summary>
		/// Message id to replay.
		/// </summary>
		public string MessageId { get; set; }

		/// <summary>
		/// Character display name.
		/// </summary>
		public string CharacterName { get; set; }

		/// <summary>
		/// Message text to synthesize.
		/// </summary>
		public string Text { get; set; }

		/// <summary>
		/// Optional tone hint.
		/// </summary>
		public string Tone { get; set; }

		/// <summary>
		/// Whether to force TTS regeneration.
		/// </summary>
		public bool ForceReload { get; set; }

		/// <summary>
		/// View-local message index for reloading UI state tracking.
		/// </summary>
		public int MessageIndex { get; set; } = -1;

		/// <summary>
		/// Emotion label for this turn (e.g. happy, sad, neutral).
		/// </summary>
		public string Emotion { get; set; }

		/// <summary>
		/// Emotional intensity for this turn: low | medium | high.
		/// </summary>
		public string Intensity { get; set; }
	}

	/// <summary>
	/// Event payload from controller to view for playing message audio.
	/// </summary>
	public sealed class ChatPlayMessageAudioPayload
	{
		/// <summary>
		/// Message id to replay.
		/// </summary>
		public string MessageId { get; set; }

		/// <summary>
		/// Character display name.
		/// </summary>
		public string CharacterName { get; set; }

		/// <summary>
		/// Message text to synthesize.
		/// </summary>
		public string Text { get; set; }

		/// <summary>
		/// Tone hint used for TTS.
		/// </summary>
		public string Tone { get; set; }

		/// <summary>
		/// Voice name resolved by controller from cache/signals.
		/// </summary>
		public string VoiceName { get; set; }

		/// <summary>
		/// Character pitch value from cache.
		/// </summary>
		public float? Pitch { get; set; }

		/// <summary>
		/// Character speaking rate value from cache.
		/// </summary>
		public float? SpeakingRate { get; set; }

		/// <summary>
		/// Pre-resolved absolute TTS audio URL.
		/// </summary>
		public string AudioUrl { get; set; }

		/// <summary>
		/// Pre-downloaded audio clip for playback (populated by controller).
		/// </summary>
		[JsonIgnore]
		public UnityEngine.AudioClip AudioClip { get; set; }

		/// <summary>
		/// Whether this playback was a forced re-generation.
		/// </summary>
		public bool ForceReload { get; set; }

		/// <summary>
		/// View-local message index for reloading UI state tracking.
		/// </summary>
		public int MessageIndex { get; set; } = -1;

		/// <summary>
		/// Emotion label for this turn.
		/// </summary>
		public string Emotion { get; set; }

		/// <summary>
		/// Emotional intensity for this turn.
		/// </summary>
		public string Intensity { get; set; }
	}

	/// <summary>
	/// One role/content message entry returned by chat APIs.
	/// </summary>
	public sealed class ChatHistoryMessagePayload
	{
		/// <summary>
		/// Role value from server: user or assistant.
		/// </summary>
		[JsonProperty("role")]
		public string Role { get; set; }

		/// <summary>
		/// Message text content.
		/// </summary>
		[JsonProperty("content")]
		public string Content { get; set; }

		/// <summary>
		/// Pre-parsed assistant turns (populated by Controller, not deserialized from server).
		/// </summary>
		[JsonIgnore]
		public System.Collections.Generic.List<ChatAssistantTurnPayload> Turns { get; set; }
	}

	/// <summary>
	/// Response payload for history endpoint.
	/// </summary>
	public sealed class ChatHistoryResponsePayload
	{
		/// <summary>
		/// Message list excluding system/developer entries.
		/// </summary>
		[JsonProperty("messages")]
		public System.Collections.Generic.List<ChatHistoryMessagePayload> Messages { get; set; } = new System.Collections.Generic.List<ChatHistoryMessagePayload>();
	}

	/// <summary>
	/// Response payload for send endpoint.
	/// </summary>
	public sealed class ChatSendResponsePayload
	{
		/// <summary>
		/// Assistant reply text.
		/// </summary>
		[JsonProperty("reply")]
		public string Reply { get; set; }

		/// <summary>
		/// Effective model used by server.
		/// </summary>
		[JsonProperty("model")]
		public string Model { get; set; }

		/// <summary>
		/// Error or status message from server.
		/// </summary>
		[JsonProperty("message")]
		public string Message { get; set; }

		/// <summary>
		/// Transcription of user audio when an audio recording was sent.
		/// Populated by Gemini's analysis of the audio content.
		/// </summary>
		[JsonProperty("transcribe")]
		public string Transcribe { get; set; }
	}

	/// <summary>
	/// Response payload for ending a conversation and creating a journal.
	/// </summary>
	public sealed class ChatEndConversationResponsePayload
	{
		/// <summary>
		/// Newly created journal id.
		/// </summary>
		[JsonProperty("journalId")]
		public int JournalId { get; set; }

		/// <summary>
		/// Summary returned by server.
		/// </summary>
		[JsonProperty("summary")]
		public string Summary { get; set; }
	}

	/// <summary>
	/// Request payload for ending a conversation with a pre-computed local AI summary.
	/// </summary>
	public sealed class ChatEndConversationLocalRequestPayload
	{
		/// <summary>
		/// Pre-computed conversation summary from local AI.
		/// </summary>
		[JsonProperty("summary")]
		public string Summary { get; set; }

		/// <summary>
		/// Optional updated story description from local AI.
		/// </summary>
		[JsonProperty("updatedStoryDescription")]
		public string UpdatedStoryDescription { get; set; }
	}

	/// <summary>
	/// Ollama summary response parsed from JSON output.
	/// </summary>
	public sealed class OllamaSummaryResult
	{
		/// <summary>
		/// Summary of the conversation in Vietnamese.
		/// </summary>
		[JsonProperty("Summary")]
		public string Summary { get; set; }

		/// <summary>
		/// Updated story description in Vietnamese.
		/// </summary>
		[JsonProperty("UpdatedStoryDescription")]
		public string UpdatedStoryDescription { get; set; }
	}

	/// <summary>
	/// Response payload for /api/text-to-speech.
	/// </summary>
	public sealed class ChatTextToSpeechResponsePayload
	{
		/// <summary>
		/// Generated audio id on server.
		/// </summary>
		[JsonProperty("output")]
		public string Output { get; set; }

		/// <summary>
		/// URL to the generated audio file.
		/// </summary>
		[JsonProperty("url")]
		public string Url { get; set; }

		/// <summary>
		/// Whether the text was rewritten by the server for TTS compatibility.
		/// </summary>
		[JsonProperty("rewritten")]
		public bool Rewritten { get; set; }

		/// <summary>
		/// Rewritten text (only present when <see cref="Rewritten"/> is true).
		/// </summary>
		[JsonProperty("text")]
		public string Text { get; set; }

		/// <summary>
		/// Rewritten pinyin (only present when <see cref="Rewritten"/> is true).
		/// </summary>
		[JsonProperty("pinyin")]
		public string Pinyin { get; set; }
	}

	public sealed class ChatCheckAudioResponsePayload
	{
		[JsonProperty("exists")]
		public bool Exists { get; set; }
		[JsonProperty("audioId")]
		public string AudioId { get; set; }
		[JsonProperty("url")]
		public string Url { get; set; }

		[JsonProperty("message")]
		public string Message { get; set; }
		[JsonProperty("error")]
		public string Error { get; set; }
	}

	/// <summary>
	/// Event payload for notifying views that a message's content was rewritten by the TTS service.
	/// </summary>
	public sealed class ChatMessageContentUpdatedPayload
	{
		/// <summary>
		/// Identifier of the message whose content was updated.
		/// </summary>
		public string MessageId { get; set; }

		/// <summary>
		/// New text after rewrite.
		/// </summary>
		public string Text { get; set; }

		/// <summary>
		/// New pinyin after rewrite.
		/// </summary>
		public string Pinyin { get; set; }
	}

	/// <summary>
	/// Character entry returned by /api/characters.
	/// </summary>
	public sealed class ChatCharacterPayload
	{
		/// <summary>
		/// Character display name.
		/// </summary>
		[JsonProperty("name")]
		public string Name { get; set; }

		/// <summary>
		/// Character avatar URL.
		/// </summary>
		[JsonProperty("avatar")]
		public string Avatar { get; set; }
	}

	/// <summary>
	/// One assistant turn in JSON reply format.
	/// </summary>
	public sealed class ChatAssistantTurnPayload
	{
		/// <summary>
		/// Unique message id.
		/// </summary>
		[JsonProperty("MessageId")]
		public string MessageId { get; set; }

		/// <summary>
		/// Character speaking this turn.
		/// </summary>
		[JsonProperty("CharacterName")]
		public string CharacterName { get; set; }

		/// <summary>
		/// Main chat text.
		/// </summary>
		[JsonProperty("Text")]
		public string Text { get; set; }

		/// <summary>
		/// Context of the action/situation.
		/// </summary>
		[JsonProperty("Context")]
		public string Context { get; set; }

		/// <summary>
		/// Pinyin reading of the text.
		/// </summary>
		[JsonProperty("Pinyin")]
		public string Pinyin { get; set; }

		/// <summary>
		/// Tone hint from server.
		/// </summary>
		[JsonProperty("Tone")]
		public string Tone { get; set; }

		/// <summary>
		/// Translation text from server.
		/// </summary>
		[JsonProperty("Translation")]
		public string Translation { get; set; }

		/// <summary>
		/// Pre-resolved TTS audio URL (populated by controller before publishing to view).
		/// </summary>
		[JsonIgnore]
		public string AudioUrl { get; set; }

		/// <summary>
		/// Pre-downloaded audio clip (populated by controller before publishing to view).
		/// </summary>
		[JsonIgnore]
		public UnityEngine.AudioClip AudioClip { get; set; }

		/// <summary>
		/// Indicates whether controller has finished attempting TTS preload for this turn.
		/// </summary>
		[JsonIgnore]
		public bool IsAudioPreloadCompleted { get; set; }

		/// <summary>
		/// Transcription of user audio when an audio recording was sent.
		/// </summary>
		[JsonProperty("Transcribe")]
		public string Transcribe { get; set; }

		/// <summary>
		/// Emotion label for this turn (e.g. happy, sad, neutral).
		/// One of: angry, shouting, disgusted, sad, scared, surprised, shy, affectionate, happy, excited, serious, neutral.
		/// </summary>
		[JsonProperty("Emotion")]
		public string Emotion { get; set; }

		/// <summary>
		/// Emotional intensity for this turn: low | medium | high.
		/// </summary>
		[JsonProperty("Intensity")]
		public string Intensity { get; set; }
	}

	/// <summary>
	/// Event payload for a newly received assistant reply.
	/// </summary>
	public sealed class ChatAssistantMessagePayload
	{
		/// <summary>
		/// Assistant reply text.
		/// </summary>
		public string Reply { get; set; }

		/// <summary>
		/// Effective model returned by server.
		/// </summary>
		public string Model { get; set; }

		/// <summary>
		/// Optional session id associated with the reply.
		/// </summary>
		public string SessionId { get; set; }

		/// <summary>
		/// Parsed assistant turns when reply is JSON array/object.
		/// </summary>
		public System.Collections.Generic.List<ChatAssistantTurnPayload> Turns { get; set; } = new System.Collections.Generic.List<ChatAssistantTurnPayload>();

		/// <summary>
		/// Transcription of user audio when the message contained a voice recording.
		/// </summary>
		public string Transcribe { get; set; }
	}

	/// <summary>
	/// Event payload for request failures.
	/// </summary>
	public sealed class ChatErrorPayload
	{
		/// <summary>
		/// Human-readable error message.
		/// </summary>
		public string Message { get; set; }
	}

	/// <summary>
	/// Character item payload for add-character popup in chat.
	/// </summary>
	public sealed class ChatSelectableCharacterPayload
	{
		/// <summary>
		/// Character display name.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Character avatar sprite.
		/// </summary>
		public Sprite Avatar { get; set; }

		/// <summary>
		/// Indicates whether this character is active in current chat context.
		/// </summary>
		public bool IsActive { get; set; }
	}

	/// <summary>
	/// Response payload from /api/chat/developer-state.
	/// </summary>
	public sealed class ChatDeveloperStatePayload
	{
		/// <summary>
		/// Active character names parsed from developer messages.
		/// </summary>
		[JsonProperty("activeCharacterNames")]
		public System.Collections.Generic.List<string> ActiveCharacterNames { get; set; }
	}

	/// <summary>
	/// Request payload from view when character toggle changes in popup.
	/// </summary>
	public sealed class ChatSetCharacterActiveRequestPayload
	{
		/// <summary>
		/// Optional chat session id.
		/// </summary>
		public string SessionId { get; set; }

		/// <summary>
		/// Character name from popup item.
		/// </summary>
		public string CharacterName { get; set; }

		/// <summary>
		/// True if character is selected (toggle on), false if removed (toggle off).
		/// </summary>
		public bool IsActive { get; set; }
	}

	/// <summary>
	/// Request payload from view to save chat context via developer API.
	/// </summary>
	public sealed class ChatSaveContextRequestPayload
	{
		/// <summary>
		/// Optional chat session id.
		/// </summary>
		public string SessionId { get; set; }

		/// <summary>
		/// Context text to append into developer history.
		/// </summary>
		public string Context { get; set; }
	}

	/// <summary>
	/// API payload for /api/chat/developer endpoint.
	/// </summary>
	public sealed class ChatDeveloperMessageRequestPayload
	{
		[JsonProperty("sessionId")]
		public string SessionId { get; set; }

		[JsonProperty("kind")]
		public string Kind { get; set; }

		[JsonProperty("character")]
		public ChatDeveloperMessageCharacterPayload Character { get; set; }

		[JsonProperty("context")]
		public string Context { get; set; }
	}

	/// <summary>
	/// Character object used in /api/chat/developer payload.
	/// </summary>
	public sealed class ChatDeveloperMessageCharacterPayload
	{
		[JsonProperty("name")]
		public string Name { get; set; }

		[JsonProperty("age")]
		public int? Age { get; set; }

		[JsonProperty("personality")]
		public string Personality { get; set; }

		[JsonProperty("gender")]
		public string Gender { get; set; }

		[JsonProperty("appearance")]
		public string Appearance { get; set; }
	}

	/// <summary>
	/// Request payload from view to controller for speech-to-text transcription.
	/// </summary>
	public sealed class ChatTranscribeAudioRequestPayload
	{
		/// <summary>
		/// Base64-encoded audio data URL (e.g. data:audio/wav;base64,...).
		/// </summary>
		public string AudioBase64 { get; set; }

		/// <summary>
		/// Language hint (e.g. ko, vi, en).
		/// </summary>
		public string Language { get; set; }
	}

	/// <summary>
	/// Request payload for /api/chat/transcribe.
	/// </summary>
	public sealed class ChatSpeechToTextRequestPayload
	{
		/// <summary>
		/// Base64 audio data URL.
		/// </summary>
		[JsonProperty("audio")]
		public string Audio { get; set; }

		/// <summary>
		/// Optional language hint (for example: vi, ko, en).
		/// </summary>
		[JsonProperty("language")]
		public string Language { get; set; }
	}

	/// <summary>
	/// Response payload from /api/chat/transcribe.
	/// </summary>
	public sealed class ChatSpeechToTextResponsePayload
	{
		/// <summary>
		/// Transcribed plain text.
		/// </summary>
		[JsonProperty("transcript")]
		public string Transcript { get; set; }
	}

	/// <summary>
	/// Event payload when Gemini transcribes an audio recording sent by the user.
	/// Published so the view can update the user message bubble with the transcribed text.
	/// </summary>
	public sealed class ChatAudioTranscribedPayload
	{
		/// <summary>
		/// Transcribed text from the audio recording.
		/// </summary>
		public string Transcribe { get; set; }

		/// <summary>
		/// The message id assigned to the user bubble in the view.
		/// </summary>
		public string UserMessageId { get; set; }
	}

	/// <summary>
	/// Request payload for looking up a vocabulary word from chat.
	/// </summary>
	public sealed class ChatVocabLookupRequestPayload
	{
		/// <summary>
		/// The Chinese word to look up.
		/// </summary>
		public string Word { get; set; }
	}

	/// <summary>
	/// Response payload from the vocabulary lookup endpoint.
	/// </summary>
	public sealed class ChatVocabLookupResponsePayload
	{
		/// <summary>
		/// Vocabulary ID from server.
		/// </summary>
		[JsonProperty("id")]
		public string Id { get; set; }

		/// <summary>
		/// Chinese word.
		/// </summary>
		[JsonProperty("korean")]
		public string Korean { get; set; }

		/// <summary>
		/// Vietnamese meaning.
		/// </summary>
		[JsonProperty("vietnamese")]
		public string Vietnamese { get; set; }

		/// <summary>
		/// Pinyin reading.
		/// </summary>
		[JsonProperty("pinyin")]
		public string Pinyin { get; set; }

		/// <summary>
		/// Whether this word was newly created during lookup.
		/// </summary>
		[JsonProperty("isNew")]
		public bool IsNew { get; set; }
	}

	/// <summary>
	/// Request payload for reviewing a vocabulary word from the chat popup.
	/// </summary>
	public sealed class ChatVocabReviewRequestPayload
	{
		/// <summary>
		/// Vocabulary ID.
		/// </summary>
		public string VocabularyId { get; set; }

		/// <summary>
		/// FSRS rating (1 = Again, 2 = Hard, 3 = Good, 4 = Easy).
		/// </summary>
		public int Rating { get; set; }
	}

	/// <summary>
	/// Event payload when a vocabulary lookup result is ready for the popup.
	/// </summary>
	public sealed class ChatVocabLookupResultPayload
	{
		/// <summary>
		/// Vocabulary ID from server.
		/// </summary>
		public string Id { get; set; }

		/// <summary>
		/// Chinese word.
		/// </summary>
		public string Word { get; set; }

		/// <summary>
		/// Vietnamese meaning.
		/// </summary>
		public string Vietnamese { get; set; }

		/// <summary>
		/// Pinyin reading.
		/// </summary>
		public string Pinyin { get; set; }

		/// <summary>
		/// Whether this word was newly created during lookup.
		/// </summary>
		public bool IsNew { get; set; }
	}

	/// <summary>
	/// Response payload for learned vocabulary count.
	/// </summary>
	public sealed class ChatVocabCountPayload
	{
		/// <summary>
		/// Number of words reviewed at least once.
		/// </summary>
		[JsonProperty("count")]
		public int Count { get; set; }
	}

	/// <summary>
	/// One learning path item used to build vocabulary marker candidates.
	/// </summary>
	public sealed class ChatLearningPathPayload
	{
		[JsonProperty("id")]
		public int Id { get; set; }

		[JsonProperty("vocabulary")]
		public string Vocabulary { get; set; }
	}

	/// <summary>
	/// Response payload for /api/learning-paths.
	/// </summary>
	public sealed class ChatLearningPathListResponsePayload
	{
		[JsonProperty("learningPaths")]
		public System.Collections.Generic.List<ChatLearningPathPayload> LearningPaths { get; set; }
			= new System.Collections.Generic.List<ChatLearningPathPayload>();
	}

	/// <summary>
	/// Minimal vocabulary item payload for /api/vocabulary list endpoint.
	/// </summary>
	public sealed class ChatVocabularyListItemPayload
	{
		[JsonProperty("korean")]
		public string Korean { get; set; }
	}

	/// <summary>
	/// Response payload for /api/vocabulary.
	/// </summary>
	public sealed class ChatVocabularyListResponsePayload
	{
		[JsonProperty("vocabularies")]
		public System.Collections.Generic.List<ChatVocabularyListItemPayload> Vocabularies { get; set; }
			= new System.Collections.Generic.List<ChatVocabularyListItemPayload>();
	}

	/// <summary>
	/// Response payload from /api/chat/prepare-local.
	/// Contains the system prompt and history for local AI generation.
	/// </summary>
	public sealed class ChatPrepareLocalResponsePayload
	{
		/// <summary>
		/// Server-built system instruction prompt.
		/// </summary>
		[JsonProperty("systemPrompt")]
		public string SystemPrompt { get; set; }

		/// <summary>
		/// Chat history excluding developer messages.
		/// </summary>
		[JsonProperty("history")]
		public System.Collections.Generic.List<ChatHistoryMessagePayload> History { get; set; }
			= new System.Collections.Generic.List<ChatHistoryMessagePayload>();

		/// <summary>
		/// Active character names in the current scene.
		/// </summary>
		[JsonProperty("activeCharacters")]
		public System.Collections.Generic.List<string> ActiveCharacters { get; set; }
			= new System.Collections.Generic.List<string>();
	}

	/// <summary>
	/// Request payload for saving a locally-generated AI reply to server history.
	/// </summary>
	public sealed class ChatSaveLocalRequestPayload
	{
		/// <summary>
		/// User message text (may be empty for respond-from-history).
		/// </summary>
		[JsonProperty("message")]
		public string Message { get; set; }

		/// <summary>
		/// AI-generated reply JSON string from local Ollama.
		/// </summary>
		[JsonProperty("reply")]
		public string Reply { get; set; }
	}

	/// <summary>
	/// Response payload from /api/chat/save-local.
	/// </summary>
	public sealed class ChatSaveLocalResponsePayload
	{
		/// <summary>
		/// True when history was saved successfully.
		/// </summary>
		[JsonProperty("ok")]
		public bool Ok { get; set; }

		/// <summary>
		/// Cleaned reply with memory sidecars stripped.
		/// </summary>
		[JsonProperty("reply")]
		public string Reply { get; set; }
	}

	/// <summary>
	/// Single message object for Ollama chat API.
	/// </summary>
	public sealed class OllamaChatMessage
	{
		/// <summary>
		/// Role: system, user, or assistant.
		/// </summary>
		[JsonProperty("role")]
		public string Role { get; set; }

		/// <summary>
		/// Message content.
		/// </summary>
		[JsonProperty("content")]
		public string Content { get; set; }
	}

	/// <summary>
	/// Request payload for Ollama /api/chat endpoint.
	/// </summary>
	public sealed class OllamaChatRequestPayload
	{
		/// <summary>
		/// Model name (e.g. gemma4:e4b).
		/// </summary>
		[JsonProperty("model")]
		public string Model { get; set; }

		/// <summary>
		/// Conversation messages including system prompt.
		/// </summary>
		[JsonProperty("messages")]
		public System.Collections.Generic.List<OllamaChatMessage> Messages { get; set; }
			= new System.Collections.Generic.List<OllamaChatMessage>();

		/// <summary>
		/// Whether to stream the response. Always false for this integration.
		/// </summary>
		[JsonProperty("stream")]
		public bool Stream { get; set; }

		/// <summary>
		/// Optional format override. Set to "json" to enforce strict JSON output.
		/// </summary>
		[JsonProperty("format", NullValueHandling = NullValueHandling.Ignore)]
		public string Format { get; set; }
	}

	/// <summary>
	/// Response payload from Ollama /api/chat endpoint (non-streaming).
	/// </summary>
	public sealed class OllamaChatResponsePayload
	{
		/// <summary>
		/// Model that generated the response.
		/// </summary>
		[JsonProperty("model")]
		public string Model { get; set; }

		/// <summary>
		/// Assistant message from Ollama.
		/// </summary>
		[JsonProperty("message")]
		public OllamaChatMessage Message { get; set; }

		/// <summary>
		/// Whether the response generation is complete.
		/// </summary>
		[JsonProperty("done")]
		public bool Done { get; set; }
	}

	/// <summary>
	/// Payload published when auto-chat vocabulary has been loaded from server.
	/// Contains due (old/review) words and non-due (new) words.
	/// </summary>
	public sealed class ChatAutoChatVocabularyPayload
	{
		/// <summary>
		/// Vocabulary words that are due for review (old).
		/// </summary>
		public System.Collections.Generic.List<string> DueWords { get; set; }
			= new System.Collections.Generic.List<string>();

		/// <summary>
		/// Vocabulary words that are not due (new).
		/// </summary>
		public System.Collections.Generic.List<string> NewWords { get; set; }
			= new System.Collections.Generic.List<string>();

		/// <summary>
		/// Number of vocabulary entries created today (used to cap daily new-word intake).
		/// </summary>
		public int TodayNewCount { get; set; }
	}

	/// <summary>
	/// Request payload sent to server to batch-review vocabulary words by text.
	/// </summary>
	public sealed class ChatBatchReviewVocabRequestPayload
	{
		/// <summary>
		/// List of vocabulary word strings to mark as reviewed.
		/// </summary>
		[Newtonsoft.Json.JsonProperty("words")]
		public System.Collections.Generic.List<string> Words { get; set; }
			= new System.Collections.Generic.List<string>();
	}

	/// <summary>
	/// Request payload for generating a vocabulary example sentence.
	/// </summary>
	public sealed class ChatVocabExampleRequestPayload
	{
		/// <summary>
		/// The Chinese word to generate an example sentence for.
		/// </summary>
		[Newtonsoft.Json.JsonProperty("word")]
		public string Word { get; set; }
	}

	/// <summary>
	/// Response payload from the generate-vocab-example endpoint.
	/// </summary>
	public sealed class ChatVocabExampleResponsePayload
	{
		/// <summary>
		/// The example sentence in Chinese.
		/// </summary>
		[Newtonsoft.Json.JsonProperty("sentence")]
		public string Sentence { get; set; }

		/// <summary>
		/// Pinyin reading of the sentence.
		/// </summary>
		[Newtonsoft.Json.JsonProperty("pinyin")]
		public string Pinyin { get; set; }

		/// <summary>
		/// Vietnamese translation of the sentence.
		/// </summary>
		[Newtonsoft.Json.JsonProperty("translation")]
		public string Translation { get; set; }
	}

	/// <summary>
	/// Event payload when a vocabulary example sentence has been generated.
	/// </summary>
	public sealed class ChatVocabExampleResultPayload
	{
		/// <summary>
		/// The Chinese word the example was generated for.
		/// </summary>
		public string Word { get; set; }

		/// <summary>
		/// The example sentence in Chinese.
		/// </summary>
		public string Sentence { get; set; }

		/// <summary>
		/// Pinyin reading of the sentence.
		/// </summary>
		public string Pinyin { get; set; }

		/// <summary>
		/// Vietnamese translation of the sentence.
		/// </summary>
		public string Translation { get; set; }
	}

	/// <summary>
	/// Payload published when mission vocabulary has been loaded from server.
	/// Contains due vocabulary words with their pinyin and meaning.
	/// </summary>
	public sealed class ChatMissionVocabularyPayload
	{
		public System.Collections.Generic.List<ChatMissionVocabItemPayload> Items { get; set; }
			= new System.Collections.Generic.List<ChatMissionVocabItemPayload>();
	}

	/// <summary>
	/// Single mission vocabulary item with hanzi, pinyin, and Vietnamese meaning.
	/// </summary>
	public sealed class ChatMissionVocabItemPayload
	{
		[JsonProperty("korean")]
		public string Korean { get; set; }

		[JsonProperty("pinyin")]
		public string Pinyin { get; set; }

		[JsonProperty("vietnamese")]
		public string Vietnamese { get; set; }
	}
}
