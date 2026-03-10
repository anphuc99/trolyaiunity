using System;

namespace Features.GamePlay.SubFeatures.Knowledges.Model
{
	/// <summary>
	/// Parent-provided callbacks for child-to-parent signaling.
	/// </summary>
	public sealed class KnowledgesParentSignals
	{
		/// <summary>
		/// Callback invoked when user wants to navigate back to Subjects.
		/// </summary>
		public Action OnBackToSubjects { get; set; }

		/// <summary>
		/// Callback invoked when user wants to start learning with the current subject.
		/// Subject ID is read from GlobalVariables.
		/// </summary>
		public Action OnStartLearning { get; set; }

		/// <summary>
		/// Adds a menu item and returns its identifier.
		/// </summary>
		public Func<string, Action, string> AddMenu { get; set; }

		/// <summary>
		/// Removes a menu item by identifier.
		/// </summary>
		public Action<string> RemoveMenu { get; set; }
	}
}
