using System;

namespace Features.GamePlay.SubFeatures.CreateSubjects.Model
{
	/// <summary>
	/// Parent-provided callbacks for child-to-parent signaling.
	/// </summary>
	public sealed class CreateSubjectsParentSignals
	{
		/// <summary>
		/// Callback invoked when a subject is successfully created.
		/// Parameter: the created subject id.
		/// </summary>
		public Action<int> OnSubjectCreated { get; set; }

		/// <summary>
		/// Callback invoked when the user cancels subject creation.
		/// </summary>
		public Action OnCancelled { get; set; }
	}
}
