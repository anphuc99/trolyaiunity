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
}
