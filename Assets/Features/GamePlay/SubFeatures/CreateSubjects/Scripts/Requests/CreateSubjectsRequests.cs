namespace Features.GamePlay.SubFeatures.CreateSubjects.Requests
{
	/// <summary>
	/// Request keys for the CreateSubjects subfeature.
	/// </summary>
	public static class CreateSubjectsRequests
	{
		/// <summary>
		/// Submits a new subject to the server.
		/// Payload: CreateSubjectPayload (name, description).
		/// </summary>
		public const string SubmitCreate = "game.play.create.subjects.submit.request";

		/// <summary>
		/// Cancels creation and signals the parent to navigate back.
		/// </summary>
		public const string Cancel = "game.play.create.subjects.cancel.request";
	}
}
