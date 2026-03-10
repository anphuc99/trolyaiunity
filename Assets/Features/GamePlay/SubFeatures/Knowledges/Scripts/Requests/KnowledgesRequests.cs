namespace Features.GamePlay.SubFeatures.Knowledges.Requests
{
	/// <summary>
	/// Request keys for the Knowledges subfeature.
	/// </summary>
	public static class KnowledgesRequests
	{
		/// <summary>
		/// Loads knowledges list for the current subject.
		/// </summary>
		public const string LoadKnowledges = "game.play.knowledges.load.request";

		/// <summary>
		/// Navigates back to subjects list.
		/// </summary>
		public const string BackToSubjects = "game.play.knowledges.back.request";

		/// <summary>
		/// Starts learning mode with the current subject's knowledges.
		/// </summary>
		public const string StartLearning = "game.play.knowledges.learn.request";
	}
}
