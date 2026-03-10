using System.Collections.Generic;
using Newtonsoft.Json;

namespace Features.GamePlay.SubFeatures.Knowledges.Model
{
	/// <summary>
	/// Payload representing a single knowledge item from the server.
	/// </summary>
	public sealed class KnowledgeItemPayload
	{
		/// <summary>
		/// Unique identifier of the knowledge item.
		/// </summary>
		[JsonProperty("id")]
		public int Id { get; set; }

		/// <summary>
		/// Name/title of the knowledge item.
		/// </summary>
		[JsonProperty("name")]
		public string Name { get; set; }

		/// <summary>
		/// Optional description of the knowledge item.
		/// </summary>
		[JsonProperty("description")]
		public string Description { get; set; }

		/// <summary>
		/// Subject ID this knowledge belongs to.
		/// </summary>
		[JsonProperty("subjectId")]
		public int SubjectId { get; set; }
	}

	/// <summary>
	/// Wrapper for server response containing list of knowledges.
	/// </summary>
	public sealed class KnowledgesResponsePayload
	{
		/// <summary>
		/// List of knowledge items.
		/// </summary>
		[JsonProperty("knowledges")]
		public List<KnowledgeItemPayload> Knowledges { get; set; }
	}

	/// <summary>
	/// Error payload for knowledge-related failures.
	/// </summary>
	public sealed class KnowledgesErrorPayload
	{
		/// <summary>
		/// Error message to display.
		/// </summary>
		public string Message { get; set; }
	}
}
