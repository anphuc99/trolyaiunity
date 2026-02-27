using System.Collections.Generic;
using Newtonsoft.Json;

namespace Features.GamePlay.SubFeatures.Journal.Model
{
	/// <summary>
	/// Request payload for loading journal list.
	/// </summary>
	public sealed class JournalListRequestPayload
	{
		/// <summary>
		/// Optional story id used to filter journals.
		/// </summary>
		public int? StoryId { get; set; }
	}

	/// <summary>
	/// One journal summary item returned by list endpoint.
	/// </summary>
	public sealed class JournalListItemPayload
	{
		[JsonProperty("id")]
		public int Id { get; set; }

		[JsonProperty("summary")]
		public string Summary { get; set; }

		[JsonProperty("createdAt")]
		public string CreatedAt { get; set; }
	}

	/// <summary>
	/// Response payload returned by journal list endpoint.
	/// </summary>
	public sealed class JournalListResponsePayload
	{
		[JsonProperty("journals")]
		public List<JournalListItemPayload> Journals { get; set; } = new List<JournalListItemPayload>();
	}

	/// <summary>
	/// Request payload for loading one journal detail.
	/// </summary>
	public sealed class JournalDetailRequestPayload
	{
		/// <summary>
		/// Journal id to load.
		/// </summary>
		public int JournalId { get; set; }
	}

	/// <summary>
	/// Journal summary object in detail endpoint response.
	/// </summary>
	public sealed class JournalSummaryPayload
	{
		[JsonProperty("id")]
		public int Id { get; set; }

		[JsonProperty("summary")]
		public string Summary { get; set; }

		[JsonProperty("createdAt")]
		public string CreatedAt { get; set; }
	}

	/// <summary>
	/// One chat message entry returned by journal detail endpoint.
	/// </summary>
	public sealed class JournalMessagePayload
	{
		[JsonProperty("id")]
		public string Id { get; set; }

		[JsonProperty("content")]
		public string Content { get; set; }

		[JsonProperty("characterName")]
		public string CharacterName { get; set; }

		[JsonProperty("translation")]
		public string Translation { get; set; }

		[JsonProperty("tone")]
		public string Tone { get; set; }

		[JsonProperty("audio")]
		public string Audio { get; set; }

		[JsonProperty("createdAt")]
		public string CreatedAt { get; set; }
	}

	/// <summary>
	/// Response payload returned by journal detail endpoint.
	/// </summary>
	public sealed class JournalDetailResponsePayload
	{
		[JsonProperty("journal")]
		public JournalSummaryPayload Journal { get; set; }

		[JsonProperty("messages")]
		public List<JournalMessagePayload> Messages { get; set; } = new List<JournalMessagePayload>();
	}

	/// <summary>
	/// Event payload for changing journal screen mode.
	/// </summary>
	public sealed class JournalViewModePayload
	{
		/// <summary>
		/// True when detail mode should be shown.
		/// </summary>
		public bool ShowDetail { get; set; }

		/// <summary>
		/// Optional selected journal id for detail mode.
		/// </summary>
		public int? JournalId { get; set; }
	}

	/// <summary>
	/// Event payload for request failures.
	/// </summary>
	public sealed class JournalErrorPayload
	{
		/// <summary>
		/// Human-readable error message.
		/// </summary>
		public string Message { get; set; }
	}

	/// <summary>
	/// Request payload for playing journal message audio.
	/// </summary>
	public sealed class JournalPlayMessageAudioRequestPayload
	{
		/// <summary>
		/// Message id to replay.
		/// </summary>
		public string MessageId { get; set; }

		/// <summary>
		/// Message index in the current list.
		/// </summary>
		public int MessageIndex { get; set; }

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
		/// True to force TTS regeneration on server.
		/// </summary>
		public bool ForceReload { get; set; }
	}

	/// <summary>
	/// Response payload returned by text-to-speech endpoint.
	/// </summary>
	public sealed class JournalTextToSpeechResponsePayload
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
	/// Event payload for playing journal message audio.
	/// </summary>
	public sealed class JournalPlayMessageAudioPayload
	{
		/// <summary>
		/// Message id to replay.
		/// </summary>
		public string MessageId { get; set; }

		/// <summary>
		/// Message index in the current list.
		/// </summary>
		public int MessageIndex { get; set; }

		/// <summary>
		/// Character display name.
		/// </summary>
		public string CharacterName { get; set; }

		/// <summary>
		/// Message text used for TTS.
		/// </summary>
		public string Text { get; set; }

		/// <summary>
		/// Tone hint used for TTS.
		/// </summary>
		public string Tone { get; set; }

		/// <summary>
		/// Resolved audio URL returned by server.
		/// </summary>
		public string AudioUrl { get; set; }
	}

	/// <summary>
	/// One item in the sequential playback queue, carrying all TTS and display data.
	/// </summary>
	public sealed class JournalPlaybackQueueItem
	{
		/// <summary>Journal id this message belongs to.</summary>
		public int JournalId { get; set; }

		/// <summary>Server-side message id.</summary>
		public string MessageId { get; set; }

		/// <summary>Zero-based index within the flattened playback queue.</summary>
		public int QueueIndex { get; set; }

		/// <summary>Character display name (sender).</summary>
		public string CharacterName { get; set; }

		/// <summary>Message text to synthesize via TTS.</summary>
		public string Text { get; set; }

		/// <summary>Optional tone hint for TTS.</summary>
		public string Tone { get; set; }

		/// <summary>Sender display name shown in the overlay bubble.</summary>
		public string SenderName { get; set; }

		/// <summary>Translation text, if available.</summary>
		public string Translation { get; set; }
	}

	/// <summary>
	/// Event payload published when the currently playing message changes during auto-play.
	/// </summary>
	public sealed class JournalPlaybackMessageChangedPayload
	{
		/// <summary>The queue item that is now playing.</summary>
		public JournalPlaybackQueueItem CurrentItem { get; set; }

		/// <summary>Zero-based index of the current item in the queue.</summary>
		public int CurrentIndex { get; set; }

		/// <summary>Total number of items in the queue.</summary>
		public int TotalCount { get; set; }

		/// <summary>Resolved TTS audio URL for this message, or null if not yet resolved.</summary>
		public string AudioUrl { get; set; }
	}

	/// <summary>
	/// Event payload published when floating overlay mode changes.
	/// </summary>
	public sealed class JournalFloatingModePayload
	{
		/// <summary>True when floating overlay is active.</summary>
		public bool IsFloating { get; set; }
	}

	/// <summary>
	/// Event payload published when journal selection state changes.
	/// </summary>
	public sealed class JournalSelectionChangedPayload
	{
		/// <summary>Set of currently selected journal ids.</summary>
		public HashSet<int> SelectedIds { get; set; } = new HashSet<int>();
	}

	/// <summary>
	/// Request payload for toggling journal selection.
	/// </summary>
	public sealed class JournalToggleSelectionPayload
	{
		/// <summary>Journal id to toggle.</summary>
		public int JournalId { get; set; }
	}

	/// <summary>
	/// Event payload for playback state transitions (started/stopped/paused/resumed).
	/// </summary>
	public sealed class JournalPlaybackStatePayload
	{
		/// <summary>True when playback is active (playing or paused).</summary>
		public bool IsPlaying { get; set; }

		/// <summary>True when playback is paused.</summary>
		public bool IsPaused { get; set; }

		/// <summary>Zero-based index of the current item in the queue.</summary>
		public int CurrentIndex { get; set; }

		/// <summary>Total number of items in the queue.</summary>
		public int TotalCount { get; set; }
	}
}
