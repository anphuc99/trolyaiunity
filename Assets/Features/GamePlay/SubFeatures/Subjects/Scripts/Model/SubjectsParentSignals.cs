using System;

namespace Features.GamePlay.SubFeatures.Subjects.Model
{
	/// <summary>
	/// Parent-provided callbacks for child-to-parent signaling.
	/// </summary>
	public sealed class SubjectsParentSignals
	{
		/// <summary>
		/// Callback invoked when the user selects a subject to view its knowledges.
		/// Parameter: selected subject id.
		/// </summary>
		public Action<int> OnSubjectSelected { get; set; }
	}
}
