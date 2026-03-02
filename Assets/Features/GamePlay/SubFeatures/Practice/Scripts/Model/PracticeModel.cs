using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Features.GamePlay.SubFeatures.Practice.Model
{
	/// <summary>
	/// Available practice tabs.
	/// </summary>
	public enum PracticeTabType
	{
		Review,
		Difficult,
		Starred,
		Learn
	}

	/// <summary>
	/// Payload for requesting a practice tab.
	/// </summary>
	public sealed class PracticeTabRequestPayload
	{
		public PracticeTabType Tab { get; set; }
	}

	/// <summary>
	/// Payload for requesting message context by id.
	/// </summary>
	public sealed class PracticeContextRequestPayload
	{
		public string MessageId { get; set; }
	}

	/// <summary>
	/// Payload for submitting a translation review.
	/// </summary>
	public sealed class PracticeReviewRequestPayload
	{
		public int Rating { get; set; }

		public int? CardId { get; set; }

		public string MessageId { get; set; }
	}

	/// <summary>
	/// Published response for loading a practice tab.
	/// </summary>
	public sealed class PracticeTabResponsePayload
	{
		public PracticeTabType Tab { get; set; }

		public PracticeTabSummaryPayload Summary { get; set; }

		public List<PracticePromptItemPayload> Items { get; set; } = new List<PracticePromptItemPayload>();
	}

	/// <summary>
	/// Summary payload for the practice tab header.
	/// </summary>
	public sealed class PracticeTabSummaryPayload
	{
		public string TabLabel { get; set; }

		public int CurrentCount { get; set; }

		public int TotalCount { get; set; }
	}

	/// <summary>
	/// Prompt item shown in practice UI.
	/// </summary>
	public sealed class PracticePromptItemPayload
	{
		public int? CardId { get; set; }

		public string MessageId { get; set; }

		public string Content { get; set; }

		public string Translation { get; set; }

		public string CharacterName { get; set; }

		public string JournalSummary { get; set; }

		/// <summary>
		/// Tone hint used for dynamic TTS playback.
		/// </summary>
		public string Tone { get; set; }

		public PracticeTranslationReviewPayload Review { get; set; }

		public bool IsLearnCandidate { get; set; }
	}

	/// <summary>
	/// Error payload for practice requests.
	/// </summary>
	public sealed class PracticeErrorPayload
	{
		public string Message { get; set; }
	}

	/// <summary>
	/// Translation card payload used by practice.
	/// </summary>
	public sealed class PracticeTranslationCardPayload
	{
		[JsonProperty("id")]
		public int Id { get; set; }

		[JsonProperty("messageId")]
		public string MessageId { get; set; }

		[JsonProperty("content")]
		public string Content { get; set; }

		[JsonProperty("translation")]
		public string Translation { get; set; }

		[JsonProperty("characterName")]
		public string CharacterName { get; set; }

		[JsonProperty("journalSummary")]
		public string JournalSummary { get; set; }

		[JsonProperty("review")]
		public PracticeTranslationReviewPayload Review { get; set; }

		[JsonProperty("tone")]
		public string Tone { get; set; }
	}

	/// <summary>
	/// Translation review payload.
	/// </summary>
	public sealed class PracticeTranslationReviewPayload
	{
		[JsonProperty("id")]
		public int Id { get; set; }

		[JsonProperty("translationCardId")]
		public int TranslationCardId { get; set; }

		[JsonProperty("isStarred")]
		public bool IsStarred { get; set; }

		[JsonProperty("reviewHistory")]
		public List<PracticeReviewHistoryEntry> ReviewHistory { get; set; } = new List<PracticeReviewHistoryEntry>();
	}

	/// <summary>
	/// Review history entry payload.
	/// </summary>
	public sealed class PracticeReviewHistoryEntry
	{
		[JsonProperty("rating")]
		public int Rating { get; set; }

		[JsonProperty("date")]
		public string Date { get; set; }
	}

	/// <summary>
	/// Response payload for /api/translation.
	/// </summary>
	public sealed class PracticeTranslationListResponsePayload
	{
		[JsonProperty("cards")]
		public List<PracticeTranslationCardPayload> Cards { get; set; } = new List<PracticeTranslationCardPayload>();
	}

	/// <summary>
	/// Response payload for /api/translation/due.
	/// </summary>
	public sealed class PracticeTranslationDueResponsePayload
	{
		[JsonProperty("cards")]
		public List<PracticeTranslationCardPayload> Cards { get; set; } = new List<PracticeTranslationCardPayload>();
	}

	/// <summary>
	/// Learn candidate payload used by /api/translation/learn.
	/// </summary>
	public sealed class PracticeTranslationLearnCandidatePayload
	{
		[JsonProperty("messageId")]
		public string MessageId { get; set; }

		[JsonProperty("content")]
		public string Content { get; set; }

		[JsonProperty("translation")]
		public string Translation { get; set; }

		[JsonProperty("characterName")]
		public string CharacterName { get; set; }

		[JsonProperty("journalSummary")]
		public string JournalSummary { get; set; }

		[JsonProperty("tone")]
		public string Tone { get; set; }
	}

	/// <summary>
	/// Response payload for /api/translation/learn.
	/// </summary>
	public sealed class PracticeTranslationLearnResponsePayload
	{
		[JsonProperty("candidates")]
		public List<PracticeTranslationLearnCandidatePayload> Candidates { get; set; } = new List<PracticeTranslationLearnCandidatePayload>();
	}

	/// <summary>
	/// Context message payload for /api/translation/context.
	/// </summary>
	public sealed class PracticeContextMessagePayload
	{
		[JsonProperty("messageId")]
		public string MessageId { get; set; }

		[JsonProperty("characterName")]
		public string CharacterName { get; set; }

		[JsonProperty("text")]
		public string Text { get; set; }
	}

	/// <summary>
	/// Response payload for /api/translation/context.
	/// </summary>
	public sealed class PracticeTranslationContextResponsePayload
	{
		[JsonProperty("before")]
		public List<PracticeContextMessagePayload> Before { get; set; } = new List<PracticeContextMessagePayload>();

		[JsonProperty("after")]
		public List<PracticeContextMessagePayload> After { get; set; } = new List<PracticeContextMessagePayload>();
	}

	/// <summary>
	/// Request payload for /api/translation/review.
	/// </summary>
	public sealed class PracticeTranslationReviewRequestPayload
	{
		[JsonProperty("rating")]
		public int Rating { get; set; }

		[JsonProperty("cardId")]
		public int? CardId { get; set; }

		[JsonProperty("messageId")]
		public string MessageId { get; set; }
	}

	/// <summary>
	/// Request payload for dynamic text-to-speech playback.
	/// </summary>
	public sealed class PracticeAudioRequestPayload
	{
		public string Text { get; set; }

		public string Tone { get; set; }

		public string CharacterName { get; set; }
	}

	/// <summary>
	/// Response payload for text-to-speech endpoint.
	/// </summary>
	public sealed class PracticeTextToSpeechResponsePayload
	{
		[JsonProperty("url")]
		public string Url { get; set; }
	}

	/// <summary>
	/// Event payload containing the resolved audio URL for playback.
	/// </summary>
	public sealed class PracticeAudioUrlPayload
	{
		/// <summary>
		/// Full audio URL ready for playback.
		/// </summary>
		public string Url { get; set; }
	}

	/// <summary>
	/// Payload for submitting a journal review rating.
	/// </summary>
	public sealed class PracticeJournalReviewRequestPayload
	{
		public int Rating { get; set; }

		public int JournalId { get; set; }
	}

	/// <summary>
	/// Server-side journal review state.
	/// </summary>
	public sealed class PracticeJournalReviewPayload
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

		[JsonProperty("reviewHistory")]
		public List<PracticeReviewHistoryEntry> ReviewHistory { get; set; } = new List<PracticeReviewHistoryEntry>();
	}

	/// <summary>
	/// A single due journal item returned by the server.
	/// </summary>
	public sealed class PracticeDueJournalItemPayload
	{
		[JsonProperty("journalId")]
		public int JournalId { get; set; }

		[JsonProperty("summary")]
		public string Summary { get; set; }

		[JsonProperty("createdAt")]
		public string CreatedAt { get; set; }

		[JsonProperty("review")]
		public PracticeJournalReviewPayload Review { get; set; }
	}

	/// <summary>
	/// Response payload for GET /api/journal-reviews/due.
	/// </summary>
	public sealed class PracticeDueJournalsResponsePayload
	{
		[JsonProperty("journals")]
		public List<PracticeDueJournalItemPayload> Journals { get; set; } = new List<PracticeDueJournalItemPayload>();

		[JsonProperty("total")]
		public int Total { get; set; }
	}

	/// <summary>
	/// Published payload when FSRS journal list is loaded.
	/// </summary>
	public sealed class PracticeFsrsJournalsLoadedPayload
	{
		public List<PracticeDueJournalItemPayload> Journals { get; set; } = new List<PracticeDueJournalItemPayload>();

		public int Total { get; set; }
	}

	/// <summary>
	/// Request payload for POST /api/journal-reviews/review.
	/// </summary>
	public sealed class PracticeJournalReviewServerRequestPayload
	{
		[JsonProperty("rating")]
		public int Rating { get; set; }

		[JsonProperty("journalId")]
		public int JournalId { get; set; }
	}
}
