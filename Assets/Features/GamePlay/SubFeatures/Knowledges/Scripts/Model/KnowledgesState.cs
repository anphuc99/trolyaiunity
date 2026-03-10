using System.Collections.Generic;

namespace Features.GamePlay.SubFeatures.Knowledges.Model
{
	/// <summary>
	/// Holds subfeature state, including parent signal bindings.
	/// </summary>
	public static class KnowledgesState
	{
		/// <summary>
		/// Parent-provided signals for cross-subfeature communication.
		/// </summary>
		public static KnowledgesParentSignals ParentSignals { get; set; }

		/// <summary>
		/// Cached list of knowledges loaded from the server.
		/// </summary>
		public static List<KnowledgeItemPayload> CachedKnowledges { get; set; }

		/// <summary>
		/// Menu item ID for the back navigation menu.
		/// </summary>
		public static string BackMenuId { get; set; }

		/// <summary>
		/// The currently selected subject ID from GlobalVariables.
		/// </summary>
		public static int CurrentSubjectId { get; set; }

		/// <summary>
		/// Resets all state to defaults.
		/// </summary>
		public static void Reset()
		{
			CachedKnowledges = null;
			BackMenuId = null;
			CurrentSubjectId = 0;
		}
	}
}
