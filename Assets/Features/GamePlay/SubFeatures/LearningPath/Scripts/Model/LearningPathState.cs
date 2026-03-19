namespace Features.GamePlay.SubFeatures.LearningPath.Model
{
	/// <summary>
	/// Holds subfeature state, including parent signal bindings.
	/// </summary>
	public static class LearningPathState
	{
		public static LearningPathParentSignals ParentSignals { get; set; }

		/// <summary>
		/// Cached learning path list from the latest successful load.
		/// </summary>
		public static LearningPathListResponsePayload CachedList { get; set; } = new LearningPathListResponsePayload();

		/// <summary>
		/// Currently editing learning path id.
		/// </summary>
		public static int? EditingLearningPathId { get; set; }
	}
}
