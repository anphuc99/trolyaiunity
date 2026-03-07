using Newtonsoft.Json;
using System.Collections.Generic;

namespace Features.GamePlay.SubFeatures.Task.Model
{
	/// <summary>
	/// Response payload for /api/tasks/today.
	/// </summary>
	public sealed class TaskTodayResponsePayload
	{
		[JsonProperty("date")]
		public string Date { get; set; }

		[JsonProperty("tasks")]
		public List<TaskTodayItemPayload> Tasks { get; set; } = new List<TaskTodayItemPayload>();

		[JsonProperty("completedCount")]
		public int CompletedCount { get; set; }

		[JsonProperty("totalCount")]
		public int TotalCount { get; set; }
	}

	/// <summary>
	/// A task item returned from /api/tasks/today.
	/// </summary>
	public sealed class TaskTodayItemPayload
	{
		[JsonProperty("id")]
		public string Id { get; set; }

		[JsonProperty("label")]
		public string Label { get; set; }

		[JsonProperty("type")]
		public string Type { get; set; }

		[JsonProperty("progress")]
		public int Progress { get; set; }

		[JsonProperty("target")]
		public int Target { get; set; }

		[JsonProperty("remaining")]
		public int Remaining { get; set; }

		[JsonProperty("completed")]
		public bool Completed { get; set; }
	}

	/// <summary>
	/// Event payload for task data shown on view.
	/// </summary>
	public sealed class TaskTodayViewPayload
	{
		public string Date { get; set; }

		public List<TaskTodayItemPayload> Tasks { get; set; } = new List<TaskTodayItemPayload>();

		public int CompletedCount { get; set; }

		public int TotalCount { get; set; }
	}

	/// <summary>
	/// Error payload for task feature requests.
	/// </summary>
	public sealed class TaskErrorPayload
	{
		public string Message { get; set; }
	}
}
