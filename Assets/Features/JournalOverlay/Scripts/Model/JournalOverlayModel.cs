using System.Collections.Generic;
using Newtonsoft.Json;

namespace Features.JournalOverlay.Model
{
	/// <summary>
	/// GlobalVariables key used to pass selected journal IDs from GamePlay → JournalOverlay.
	/// </summary>
	public static class JournalOverlayGlobalKeys
	{
		/// <summary>
		/// Key for <c>List&lt;int&gt;</c> of journal IDs selected by the user for playback.
		/// </summary>
		public const string SelectedJournalIds = "global.journal.overlay.selected.ids";
	}

	// ================================================================
	// Server response models (mirrors of Journal subfeature models —
	// kept here so JournalOverlay assembly has no dependency on GamePlay.Journal)
	// ================================================================

	/// <summary>
	/// Journal summary object returned by the detail endpoint.
	/// </summary>
	public sealed class OverlayJournalSummary
	{
		[JsonProperty("id")]
		public int Id { get; set; }

		[JsonProperty("summary")]
		public string Summary { get; set; }

		[JsonProperty("createdAt")]
		public string CreatedAt { get; set; }
	}

	/// <summary>
	/// One chat message entry returned by the journal detail endpoint.
	/// </summary>
	public sealed class OverlayJournalMessage
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
	/// Response payload returned by the journal detail endpoint.
	/// </summary>
	public sealed class OverlayJournalDetailResponse
	{
		[JsonProperty("journal")]
		public OverlayJournalSummary Journal { get; set; }

		[JsonProperty("messages")]
		public List<OverlayJournalMessage> Messages { get; set; } = new List<OverlayJournalMessage>();
	}

	/// <summary>
	/// Response payload returned by the text-to-speech endpoint.
	/// </summary>
	public sealed class OverlayTtsResponse
	{
		[JsonProperty("output")]
		public string Output { get; set; }

		[JsonProperty("url")]
		public string Url { get; set; }
	}

	/// <summary>
	/// One item in the flattened playback queue. Carries all data needed for display and TTS.
	/// </summary>
	public sealed class OverlayPlaybackQueueItem
	{
		/// <summary>Journal ID this message belongs to.</summary>
		public int JournalId { get; set; }

		/// <summary>Server-side message ID.</summary>
		public string MessageId { get; set; }

		/// <summary>Zero-based index in the playback queue.</summary>
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
	/// Event payload published when the currently playing message changes.
	/// </summary>
	public sealed class OverlayPlaybackMessagePayload
	{
		/// <summary>The queue item that is now playing.</summary>
		public OverlayPlaybackQueueItem CurrentItem { get; set; }

		/// <summary>Zero-based index of the current item.</summary>
		public int CurrentIndex { get; set; }

		/// <summary>Total items in the queue.</summary>
		public int TotalCount { get; set; }

		/// <summary>Resolved TTS audio URL, or null if unavailable.</summary>
		public string AudioUrl { get; set; }
	}

	/// <summary>
	/// Event payload for playback state transitions.
	/// </summary>
	public sealed class OverlayPlaybackStatePayload
	{
		/// <summary>True while playback is active.</summary>
		public bool IsPlaying { get; set; }

		/// <summary>True when playback is paused.</summary>
		public bool IsPaused { get; set; }

		/// <summary>Zero-based index of the current item.</summary>
		public int CurrentIndex { get; set; }

		/// <summary>Total items in the queue.</summary>
		public int TotalCount { get; set; }
	}

	/// <summary>
	/// Event payload for errors.
	/// </summary>
	public sealed class OverlayErrorPayload
	{
		/// <summary>Human-readable error message.</summary>
		public string Message { get; set; }
	}
}
