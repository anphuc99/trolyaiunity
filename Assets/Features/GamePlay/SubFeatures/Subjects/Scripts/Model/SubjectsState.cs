using System.Collections.Generic;

namespace Features.GamePlay.SubFeatures.Subjects.Model
{
	/// <summary>
	/// Holds subfeature state, including parent signal bindings and cached data.
	/// </summary>
	public static class SubjectsState
	{
		/// <summary>
		/// Parent-provided signals for child-to-parent communication.
		/// </summary>
		public static SubjectsParentSignals ParentSignals { get; set; }

		/// <summary>
		/// Cached list of subjects loaded from the server.
		/// </summary>
		public static List<SubjectItemPayload> CachedSubjects { get; set; } = new List<SubjectItemPayload>();

		/// <summary>
		/// Parent menu item id for "create subject" action.
		/// </summary>
		public static string CreateSubjectMenuId { get; set; }

		/// <summary>
		/// Resets all cached state to defaults.
		/// </summary>
		public static void Reset()
		{
			CachedSubjects = new List<SubjectItemPayload>();
			CreateSubjectMenuId = null;
		}
	}
}
