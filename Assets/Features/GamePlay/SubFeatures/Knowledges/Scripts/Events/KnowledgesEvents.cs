namespace Features.GamePlay.SubFeatures.Knowledges.Events
{
	/// <summary>
	/// Event keys for the Knowledges subfeature.
	/// </summary>
	public static class KnowledgesEvents
	{
		public const string Echoed = "game.play.knowledges.echo.event";

		/// <summary>
		/// Published when the Knowledges subfeature is installed and visible.
		/// </summary>
		public const string Installed = "game.play.knowledges.installed.event";

		/// <summary>
		/// Published when the Knowledges subfeature is uninstalled and hidden.
		/// </summary>
		public const string Uninstalled = "game.play.knowledges.uninstalled.event";
	}
}
