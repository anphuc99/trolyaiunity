namespace Features.GamePlay.SubFeatures.Journal.Model
{
	/// <summary>
	/// Holds subfeature state, including parent signal bindings.
	/// </summary>
	public static class JournalState
	{
		public static JournalParentSignals ParentSignals { get; set; }

		/// <summary>
		/// Cached journal list from latest successful request.
		/// </summary>
		public static JournalListResponsePayload CachedList { get; set; } = new JournalListResponsePayload();

		/// <summary>
		/// Cached journal detail from latest successful request.
		/// </summary>
		public static JournalDetailResponsePayload CachedDetail { get; set; }

		/// <summary>
		/// Last selected journal id.
		/// </summary>
		public static int? SelectedJournalId { get; set; }
	}
}
