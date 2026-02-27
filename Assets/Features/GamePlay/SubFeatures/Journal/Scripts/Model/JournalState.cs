using System.Collections.Generic;

namespace Features.GamePlay.SubFeatures.Journal.Model
{
	/// <summary>
	/// Holds subfeature state, including parent signal bindings.
	/// Playback state has been moved to JournalOverlay feature.
	/// </summary>
	public static class JournalState
	{
		public static JournalParentSignals ParentSignals { get; set; }

		/// <summary>
		/// Cached journal list from latest successful request.
		/// </summary>
		public static JournalListResponsePayload CachedList { get; set; } = new JournalListResponsePayload();

		/// <summary>
		/// Cached journal detail from latest successful request.
		/// </summary>
		public static JournalDetailResponsePayload CachedDetail { get; set; }

		/// <summary>
		/// Last selected journal id.
		/// </summary>
		public static int? SelectedJournalId { get; set; }

		// --- Multi-select state ---

		/// <summary>
		/// Set of journal ids selected for batch playback via JournalOverlay.
		/// </summary>
		public static HashSet<int> SelectedJournalIds { get; set; } = new HashSet<int>();

		/// <summary>
		/// Resets all selection state to defaults.
		/// </summary>
		public static void ResetAll()
		{
			SelectedJournalIds.Clear();
		}
	}
}
