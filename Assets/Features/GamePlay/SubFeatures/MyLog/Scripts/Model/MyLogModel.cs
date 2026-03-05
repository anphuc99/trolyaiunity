using System.Collections.Generic;

namespace Features.GamePlay.SubFeatures.MyLog.Model
{
	/// <summary>
	/// One log item in MyLog.
	/// </summary>
	public sealed class MyLogPayload
	{
		public int Id { get; set; }
		public string Content { get; set; }
		public string NextReviewDate { get; set; }
		public int ReviewCount { get; set; }
		public bool IsArchived { get; set; }
		public string CreatedAt { get; set; }
		public string UpdatedAt { get; set; }
	}

	/// <summary>
	/// Response payload for listing logs.
	/// </summary>
	public sealed class MyLogListResponsePayload
	{
		public List<MyLogPayload> Logs { get; set; } = new List<MyLogPayload>();
		public int Total { get; set; }
		public int Page { get; set; }
		public int Limit { get; set; }
		public bool HasMore { get; set; }
	}

	/// <summary>
	/// Request payload for creating a new log.
	/// </summary>
	public sealed class MyLogCreateRequestPayload
	{
		public string Content { get; set; }
	}

	/// <summary>
	/// Request payload for updating an existing log.
	/// </summary>
	public sealed class MyLogUpdateRequestPayload
	{
		public int LogId { get; set; }
		public string Content { get; set; }
	}

	/// <summary>
	/// Request payload for selecting a log to edit.
	/// </summary>
	public sealed class MyLogEditRequestPayload
	{
		public int LogId { get; set; }
	}

	/// <summary>
	/// Event payload when one log is loaded into edit form.
	/// </summary>
	public sealed class MyLogEditPayload
	{
		public int LogId { get; set; }
		public string Content { get; set; }
	}

	/// <summary>
	/// Event payload after a log is saved.
	/// </summary>
	public sealed class MyLogSavedPayload
	{
		public int LogId { get; set; }
		public bool IsUpdate { get; set; }
	}

	/// <summary>
	/// Event payload for request failures.
	/// </summary>
	public sealed class MyLogErrorPayload
	{
		public string Message { get; set; }
	}
}
