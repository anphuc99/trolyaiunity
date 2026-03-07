namespace Features.GamePlay.SubFeatures.MyLog.Model
{
	/// <summary>
	/// Holds subfeature state, including parent signal bindings.
	/// </summary>
	public static class MyLogState
	{
		public static MyLogParentSignals ParentSignals { get; set; }

		public static string ChatMenuId { get; set; }

		public static string JournalMenuId { get; set; }

		/// <summary>
		/// Cached log list from latest load or save operations.
		/// </summary>
		public static MyLogListResponsePayload CachedList { get; set; } = new MyLogListResponsePayload();

		/// <summary>
		/// Currently editing log id.
		/// </summary>
		public static int? EditingLogId { get; set; }
	}
}
