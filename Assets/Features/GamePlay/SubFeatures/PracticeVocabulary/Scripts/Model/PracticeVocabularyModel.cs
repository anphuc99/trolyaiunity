using Newtonsoft.Json;
using System.Collections.Generic;

namespace Features.GamePlay.SubFeatures.PracticeVocabulary.Model
{
	/// <summary>
	/// Request payload for submitting a vocabulary FSRS review.
	/// </summary>
	public sealed class PracticeVocabularyReviewRequestPayload
	{
		public string VocabularyId { get; set; }

		public int Rating { get; set; }
	}

	/// <summary>
	/// API response payload for due vocabulary list.
	/// </summary>
	public sealed class PracticeVocabularyDueListResponsePayload
	{
		[JsonProperty("vocabularies")]
		public List<PracticeVocabularyItemPayload> Vocabularies { get; set; } = new List<PracticeVocabularyItemPayload>();

		[JsonProperty("total")]
		public int Total { get; set; }
	}

	/// <summary>
	/// One vocabulary item used by review popup flow.
	/// </summary>
	public sealed class PracticeVocabularyItemPayload
	{
		[JsonProperty("id")]
		public string Id { get; set; }

		[JsonProperty("korean")]
		public string Korean { get; set; }

		[JsonProperty("vietnamese")]
		public string Vietnamese { get; set; }

		[JsonProperty("pinyin")]
		public string Pinyin { get; set; }

		[JsonProperty("review")]
		public PracticeVocabularyReviewPayload Review { get; set; }
	}

	/// <summary>
	/// Minimal review payload returned by vocabulary review endpoints.
	/// </summary>
	public sealed class PracticeVocabularyReviewPayload
	{
		[JsonProperty("id")]
		public string Id { get; set; }

		[JsonProperty("vocabularyId")]
		public string VocabularyId { get; set; }

		[JsonProperty("nextReviewDate")]
		public string NextReviewDate { get; set; }
	}

	/// <summary>
	/// Event payload published after one review is submitted.
	/// </summary>
	public sealed class PracticeVocabularyReviewSubmittedPayload
	{
		public string VocabularyId { get; set; }

		public int Rating { get; set; }

		public PracticeVocabularyReviewPayload Review { get; set; }
	}

	/// <summary>
	/// Error payload for request failures.
	/// </summary>
	public sealed class PracticeVocabularyErrorPayload
	{
		public string Message { get; set; }
	}
}
