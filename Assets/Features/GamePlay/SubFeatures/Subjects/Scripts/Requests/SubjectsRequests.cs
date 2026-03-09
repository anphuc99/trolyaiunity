namespace Features.GamePlay.SubFeatures.Subjects.Requests
{
	/// <summary>
	/// Request keys for the Subjects subfeature.
	/// </summary>
	public static class SubjectsRequests
	{
		/// <summary>
		/// Requests loading the subjects list from the server.
		/// </summary>
		public const string LoadSubjects = "game.play.subjects.load.request";

		/// <summary>
		/// Requests opening the Knowledges subfeature for a selected subject.
		/// Payload: SubjectItemPayload (id, name, description).
		/// </summary>
		public const string SelectSubject = "game.play.subjects.select.request";

		/// <summary>
		/// Requests opening the CreateSubjects subfeature to create a new subject.
		/// </summary>
		public const string OpenCreateSubjects = "game.play.subjects.open.create.request";
	}
}
