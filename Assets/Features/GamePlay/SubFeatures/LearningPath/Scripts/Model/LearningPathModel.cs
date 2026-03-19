using System.Collections.Generic;
using Newtonsoft.Json;

namespace Features.GamePlay.SubFeatures.LearningPath.Model
{
	/// <summary>
	/// One learning path item returned by the API.
	/// </summary>
	public sealed class LearningPathPayload
	{
		[JsonProperty("id")]
		public int Id { get; set; }

		[JsonProperty("context")]
		public string Context { get; set; }

		[JsonProperty("vocabulary")]
		public string Vocabulary { get; set; }

		[JsonProperty("createdAt")]
		public string CreatedAt { get; set; }

		[JsonProperty("updatedAt")]
		public string UpdatedAt { get; set; }
	}

	/// <summary>
	/// Response payload for listing learning paths.
	/// </summary>
	public sealed class LearningPathListResponsePayload
	{
		[JsonProperty("learningPaths")]
		public List<LearningPathPayload> LearningPaths { get; set; } = new List<LearningPathPayload>();
	}

	/// <summary>
	/// Response payload for create/update learning path.
	/// </summary>
	public sealed class LearningPathSingleResponsePayload
	{
		[JsonProperty("learningPath")]
		public LearningPathPayload LearningPath { get; set; }
	}

	/// <summary>
	/// Request payload for creating a learning path.
	/// </summary>
	public sealed class LearningPathCreateRequestPayload
	{
		[JsonProperty("context")]
		public string Context { get; set; }

		[JsonProperty("vocabulary")]
		public string Vocabulary { get; set; }
	}

	/// <summary>
	/// Request payload for updating a learning path.
	/// </summary>
	public sealed class LearningPathUpdateRequestPayload
	{
		[JsonIgnore]
		public int LearningPathId { get; set; }

		[JsonProperty("context")]
		public string Context { get; set; }

		[JsonProperty("vocabulary")]
		public string Vocabulary { get; set; }
	}

	/// <summary>
	/// Request payload for editing a learning path from list selection.
	/// </summary>
	public sealed class LearningPathEditRequestPayload
	{
		public int LearningPathId { get; set; }
	}

	/// <summary>
	/// Event payload for editing a learning path.
	/// </summary>
	public sealed class LearningPathEditPayload
	{
		public int LearningPathId { get; set; }
		public string Context { get; set; }
		public string Vocabulary { get; set; }
	}

	/// <summary>
	/// Event payload for learning path save completion.
	/// </summary>
	public sealed class LearningPathSavedPayload
	{
		public int LearningPathId { get; set; }
		public bool IsUpdate { get; set; }
	}

	/// <summary>
	/// Event payload for request failures.
	/// </summary>
	public sealed class LearningPathErrorPayload
	{
		public string Message { get; set; }
	}
}
