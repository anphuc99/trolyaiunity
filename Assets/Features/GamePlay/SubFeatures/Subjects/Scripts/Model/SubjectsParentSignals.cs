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

		/// <summary>
		/// Callback invoked when the user wants to create a new subject.
		/// </summary>
		public Action OnOpenCreateSubjects { get; set; }

		/// <summary>
		/// Optional callback for adding a menu item at parent scope.
		/// </summary>
		public Func<string, Action, string> AddMenu { get; set; }

		/// <summary>
		/// Optional callback for removing a parent-scope menu item by id.
		/// </summary>
		public Action<string> RemoveMenu { get; set; }
	}
}
