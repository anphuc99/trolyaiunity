namespace Features.GamePlay.SubFeatures.Knowledges.Events
{
	/// <summary>
	/// Event keys for the Knowledges subfeature.
	/// </summary>
	public static class KnowledgesEvents
	{
		/// <summary>
		/// Published when the Knowledges subfeature is installed and visible.
		/// </summary>
		public const string Installed = "game.play.knowledges.installed.event";

		/// <summary>
		/// Published when the Knowledges subfeature is uninstalled and hidden.
		/// </summary>
		public const string Uninstalled = "game.play.knowledges.uninstalled.event";

		/// <summary>
		/// Published when knowledges are successfully loaded.
		/// Payload: List of KnowledgeItemPayload.
		/// </summary>
		public const string KnowledgesLoaded = "game.play.knowledges.loaded.event";

		/// <summary>
		/// Published when loading knowledges fails.
		/// Payload: KnowledgesErrorPayload.
		/// </summary>
		public const string KnowledgesLoadFailed = "game.play.knowledges.load.failed.event";
	}
}
