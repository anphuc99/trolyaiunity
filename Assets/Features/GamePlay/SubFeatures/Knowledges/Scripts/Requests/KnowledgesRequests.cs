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
		/// Selects a knowledge item to start learning.
		/// Payload: KnowledgeItemPayload.
		/// </summary>
		public const string SelectKnowledge = "game.play.knowledges.select.request";

		/// <summary>
		/// Navigates back to subjects list.
		/// </summary>
		public const string BackToSubjects = "game.play.knowledges.back.request";

		/// <summary>
		/// Starts learning mode with the selected knowledge.
		/// </summary>
		public const string StartLearning = "game.play.knowledges.learn.request";
	}
}
