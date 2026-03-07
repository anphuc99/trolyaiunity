using System.Collections.Generic;
using Newtonsoft.Json;

namespace Features.GamePlay.SubFeatures.Story.Model
{
	/// <summary>
	/// One story item returned by the API.
	/// </summary>
	public sealed class StoryPayload
	{
		[JsonProperty("id")]
		public int Id { get; set; }

		[JsonProperty("name")]
		public string Name { get; set; }

		[JsonProperty("description")]
		public string Description { get; set; }

		[JsonProperty("currentProgress")]
		public string CurrentProgress { get; set; }

		[JsonProperty("createdAt")]
		public string CreatedAt { get; set; }

		[JsonProperty("updatedAt")]
		public string UpdatedAt { get; set; }
	}

	/// <summary>
	/// Response payload for listing stories.
	/// </summary>
	public sealed class StoryListResponsePayload
	{
		[JsonProperty("stories")]
		public List<StoryPayload> Stories { get; set; } = new List<StoryPayload>();
	}

	/// <summary>
	/// Response payload for create/update story.
	/// </summary>
	public sealed class StorySingleResponsePayload
	{
		[JsonProperty("story")]
		public StoryPayload Story { get; set; }
	}

	/// <summary>
	/// Request payload for creating a story.
	/// </summary>
	public sealed class StoryCreateRequestPayload
	{
		[JsonProperty("name")]
		public string Name { get; set; }

		[JsonProperty("description")]
		public string Description { get; set; }

		[JsonProperty("currentProgress")]
		public string CurrentProgress { get; set; }
	}

	/// <summary>
	/// Request payload for updating a story.
	/// </summary>
	public sealed class StoryUpdateRequestPayload
	{
		[JsonIgnore]
		public int StoryId { get; set; }

		[JsonProperty("name")]
		public string Name { get; set; }

		[JsonProperty("description")]
		public string Description { get; set; }

		[JsonProperty("currentProgress")]
		public string CurrentProgress { get; set; }
	}

	/// <summary>
	/// Request payload for editing a story from list selection.
	/// </summary>
	public sealed class StoryEditRequestPayload
	{
		public int StoryId { get; set; }
	}

	/// <summary>
	/// Event payload for editing a story.
	/// </summary>
	public sealed class StoryEditPayload
	{
		public int StoryId { get; set; }
		public string Name { get; set; }
		public string Description { get; set; }
		public string CurrentProgress { get; set; }
	}

	/// <summary>
	/// Event payload for story save completion.
	/// </summary>
	public sealed class StorySavedPayload
	{
		public int StoryId { get; set; }
		public bool IsUpdate { get; set; }
	}

	/// <summary>
	/// Event payload for request failures.
	/// </summary>
	public sealed class StoryErrorPayload
	{
		public string Message { get; set; }
	}
}
