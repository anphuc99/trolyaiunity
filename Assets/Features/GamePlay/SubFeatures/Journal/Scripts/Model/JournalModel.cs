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
	/// Request payload for starting overlay playback using selected journal ids.
	/// </summary>
	public sealed class JournalStartPlaybackRequestPayload
	{
		/// <summary>
		/// List of selected journal ids.
		/// </summary>
		public List<int> SelectedIds { get; set; } = new List<int>();
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

	// ================================================================
	// FSRS Journal Review payloads
	// ================================================================

	/// <summary>
	/// One due-journal item returned by the review/due endpoint.
	/// </summary>
	public sealed class JournalDueItemPayload
	{
		[JsonProperty("id")]
		public int Id { get; set; }

		[JsonProperty("summary")]
		public string Summary { get; set; }

		[JsonProperty("createdAt")]
		public string CreatedAt { get; set; }

		[JsonProperty("review")]
		public JournalReviewPayload Review { get; set; }
	}

	/// <summary>
	/// Response payload for GET /api/journals/review/due.
	/// </summary>
	public sealed class JournalDueListResponsePayload
	{
		[JsonProperty("journals")]
		public List<JournalDueItemPayload> Journals { get; set; } = new List<JournalDueItemPayload>();
	}

	/// <summary>
	/// Review scheduling state for a journal.
	/// </summary>
	public sealed class JournalReviewPayload
	{
		[JsonProperty("id")]
		public int Id { get; set; }

		[JsonProperty("journalId")]
		public int JournalId { get; set; }

		[JsonProperty("stability")]
		public float Stability { get; set; }

		[JsonProperty("difficulty")]
		public float Difficulty { get; set; }

		[JsonProperty("lapses")]
		public int Lapses { get; set; }

		[JsonProperty("currentIntervalDays")]
		public int CurrentIntervalDays { get; set; }

		[JsonProperty("nextReviewDate")]
		public string NextReviewDate { get; set; }

		[JsonProperty("lastReviewDate")]
		public string LastReviewDate { get; set; }

		[JsonProperty("reviewHistory")]
		public List<JournalReviewHistoryEntry> ReviewHistory { get; set; } = new List<JournalReviewHistoryEntry>();
	}

	/// <summary>
	/// One entry in the journal review history.
	/// </summary>
	public sealed class JournalReviewHistoryEntry
	{
		[JsonProperty("rating")]
		public int Rating { get; set; }

		[JsonProperty("date")]
		public string Date { get; set; }
	}

	/// <summary>
	/// Request payload for submitting a journal FSRS review.
	/// </summary>
	public sealed class JournalSubmitReviewRequestPayload
	{
		/// <summary>
		/// Journal id to review.
		/// </summary>
		public int JournalId { get; set; }

		/// <summary>
		/// User rating (1=Again, 2=Hard, 3=Good, 4=Easy).
		/// </summary>
		public int Rating { get; set; }
	}

	/// <summary>
	/// Request body sent to POST /api/journals/review.
	/// </summary>
	public sealed class JournalReviewApiRequestBody
	{
		[JsonProperty("journalId")]
		public int JournalId { get; set; }

		[JsonProperty("rating")]
		public int Rating { get; set; }
	}

	/// <summary>
	/// Response payload returned by POST /api/journals/review.
	/// </summary>
	public sealed class JournalReviewApiResponsePayload
	{
		[JsonProperty("journal")]
		public JournalSummaryPayload Journal { get; set; }

		[JsonProperty("review")]
		public JournalReviewPayload Review { get; set; }
	}

}
