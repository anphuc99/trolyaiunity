using Newtonsoft.Json;

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
		/// Optional story id for prompt enrichment.
		/// </summary>
		[JsonProperty("storyId")]
		public int? StoryId { get; set; }
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
		/// Tone hint from server.
		/// </summary>
		[JsonProperty("Tone")]
		public string Tone { get; set; }

		/// <summary>
		/// Translation text from server.
		/// </summary>
		[JsonProperty("Translation")]
		public string Translation { get; set; }
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
}
