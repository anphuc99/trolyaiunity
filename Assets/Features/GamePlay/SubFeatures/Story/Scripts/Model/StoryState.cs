namespace Features.GamePlay.SubFeatures.Story.Model
{
	/// <summary>
	/// Holds subfeature state, including parent signal bindings.
	/// </summary>
	public static class StoryState
	{
		public static StoryParentSignals ParentSignals { get; set; }

		/// <summary>
		/// Cached story list from the latest successful load.
		/// </summary>
		public static StoryListResponsePayload CachedList { get; set; } = new StoryListResponsePayload();

		/// <summary>
		/// Currently editing story id.
		/// </summary>
		public static int? EditingStoryId { get; set; }
	}
}
