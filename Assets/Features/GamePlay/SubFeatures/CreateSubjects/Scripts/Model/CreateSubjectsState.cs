namespace Features.GamePlay.SubFeatures.CreateSubjects.Model
{
	/// <summary>
	/// Holds subfeature state, including parent signal bindings.
	/// </summary>
	public static class CreateSubjectsState
	{
		/// <summary>
		/// Parent-provided signals for cross-subfeature communication.
		/// </summary>
		public static CreateSubjectsParentSignals ParentSignals { get; set; }

		/// <summary>
		/// Whether a creation request is currently in progress.
		/// </summary>
		public static bool IsSubmitting { get; set; }

		/// <summary>
		/// Resets all state to defaults.
		/// </summary>
		public static void Reset()
		{
			IsSubmitting = false;
		}
	}
}
