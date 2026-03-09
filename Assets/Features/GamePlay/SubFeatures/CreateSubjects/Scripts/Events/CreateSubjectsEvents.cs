namespace Features.GamePlay.SubFeatures.CreateSubjects.Events
{
	/// <summary>
	/// Event keys for the CreateSubjects subfeature.
	/// </summary>
	public static class CreateSubjectsEvents
	{
		/// <summary>
		/// Published when the CreateSubjects subfeature is installed and visible.
		/// </summary>
		public const string Installed = "game.play.create.subjects.installed.event";

		/// <summary>
		/// Published when the CreateSubjects subfeature is uninstalled and hidden.
		/// </summary>
		public const string Uninstalled = "game.play.create.subjects.uninstalled.event";

		/// <summary>
		/// Published when subject creation is in progress.
		/// </summary>
		public const string CreateStarted = "game.play.create.subjects.create.started.event";

		/// <summary>
		/// Published when a subject is successfully created.
		/// Payload: CreateSubjectResponsePayload.
		/// </summary>
		public const string CreateSucceeded = "game.play.create.subjects.create.succeeded.event";

		/// <summary>
		/// Published when subject creation fails.
		/// Payload: CreateSubjectsErrorPayload.
		/// </summary>
		public const string CreateFailed = "game.play.create.subjects.create.failed.event";
	}
}
