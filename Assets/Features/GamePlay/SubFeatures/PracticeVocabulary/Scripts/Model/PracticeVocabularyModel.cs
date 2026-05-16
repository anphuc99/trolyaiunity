using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

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
	/// Request payload from view to controller for playing vocabulary audio.
	/// </summary>
	public sealed class PracticeVocabularyPlayAudioRequestPayload
	{
		public string CharacterName { get; set; }

		public string Text { get; set; }

		public string Tone { get; set; }

		public bool ForceReload { get; set; }
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
	/// Event payload from controller to view for vocabulary audio playback.
	/// </summary>
	public sealed class PracticeVocabularyPlayAudioPayload
	{
		public string CharacterName { get; set; }

		public string Text { get; set; }

		public string UpdatedText { get; set; }

		public string UpdatedPinyin { get; set; }

		public string Tone { get; set; }

		public string VoiceName { get; set; }

		public float? Pitch { get; set; }

		public float? SpeakingRate { get; set; }

		public string AudioUrl { get; set; }

		[JsonIgnore]
		public AudioClip AudioClip { get; set; }

		public bool ForceReload { get; set; }
	}

	/// <summary>
	/// Response payload for /api/text-to-speech.
	/// </summary>
	public sealed class PracticeVocabularyTextToSpeechResponsePayload
	{
		[JsonProperty("output")]
		public string Output { get; set; }

		[JsonProperty("url")]
		public string Url { get; set; }

		[JsonProperty("text")]
		public string Text { get; set; }

		[JsonProperty("pinyin")]
		public string Pinyin { get; set; }

		[JsonProperty("rewritten")]
		public bool Rewritten { get; set; }
	}

	/// <summary>
	/// Request payload for ignoring a vocabulary item.
	/// </summary>
	public sealed class PracticeVocabularyIgnoreRequestPayload
	{
		public string VocabularyId { get; set; }
	}

	/// <summary>
	/// Event payload published after a vocabulary is ignored.
	/// </summary>
	public sealed class PracticeVocabularyIgnoredPayload
	{
		public string VocabularyId { get; set; }
	}

	/// <summary>
	public sealed class PracticeVocabularyErrorPayload
	{
		public string Message { get; set; }
	}

	/// <summary>
	/// Request payload for generating a vocabulary example sentence.
	/// </summary>
	public sealed class PracticeVocabExampleRequestPayload
	{
		[JsonProperty("word")]
		public string Word { get; set; }
	}

	/// <summary>
	/// Response payload from the generate-vocab-example endpoint.
	/// </summary>
	public sealed class PracticeVocabExampleResponsePayload
	{
		[JsonProperty("sentence")]
		public string Sentence { get; set; }

		[JsonProperty("pinyin")]
		public string Pinyin { get; set; }

		[JsonProperty("translation")]
		public string Translation { get; set; }
	}

	/// <summary>
	/// Event payload when a vocabulary example sentence has been generated.
	/// </summary>
	public sealed class PracticeVocabExampleResultPayload
	{
		public string Word { get; set; }
		public string Sentence { get; set; }
		public string Pinyin { get; set; }
		public string Translation { get; set; }
	}
}
