namespace Features.GamePlay.SubFeatures.Subjects.Events
{
	/// <summary>
	/// Event keys for the Subjects subfeature.
	/// </summary>
	public static class SubjectsEvents
	{
		/// <summary>
		/// Published when the Subjects subfeature is installed and visible.
		/// </summary>
		public const string Installed = "game.play.subjects.installed.event";

		/// <summary>
		/// Published when the Subjects subfeature is uninstalled and hidden.
		/// </summary>
		public const string Uninstalled = "game.play.subjects.uninstalled.event";

		/// <summary>
		/// Published when subject list loading has started.
		/// </summary>
		public const string SubjectsLoadStarted = "game.play.subjects.load.started.event";

		/// <summary>
		/// Published when subject list has been loaded successfully.
		/// Payload: List&lt;SubjectItemPayload&gt;
		/// </summary>
		public const string SubjectsLoaded = "game.play.subjects.loaded.event";

		/// <summary>
		/// Published when subject list loading failed.
		/// Payload: SubjectsErrorPayload
		/// </summary>
		public const string SubjectsLoadFailed = "game.play.subjects.load.failed.event";
	}
}
