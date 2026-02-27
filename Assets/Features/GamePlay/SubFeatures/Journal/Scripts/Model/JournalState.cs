using System.Collections.Generic;

namespace Features.GamePlay.SubFeatures.Journal.Model
{
	/// <summary>
	/// Holds subfeature state, including parent signal bindings.
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

		// --- Multi-select and playback state ---

		/// <summary>
		/// Set of journal ids selected for batch playback.
		/// </summary>
		public static HashSet<int> SelectedJournalIds { get; set; } = new HashSet<int>();

		/// <summary>
		/// Flattened playlist of messages from all selected journals, ordered bottom-to-top.
		/// </summary>
		public static List<JournalPlaybackQueueItem> PlaybackQueue { get; set; } = new List<JournalPlaybackQueueItem>();

		/// <summary>
		/// Zero-based index of the message currently playing in <see cref="PlaybackQueue"/>.
		/// </summary>
		public static int CurrentPlaybackIndex { get; set; }

		/// <summary>
		/// True while sequential playback is active (playing or paused).
		/// </summary>
		public static bool IsPlaying { get; set; }

		/// <summary>
		/// True when playback is temporarily paused.
		/// </summary>
		public static bool IsPaused { get; set; }

		/// <summary>
		/// True when the Unity window is in floating overlay mode.
		/// </summary>
		public static bool IsFloatingMode { get; set; }

		/// <summary>
		/// Cache of journal details keyed by journal id, used during batch playback.
		/// </summary>
		public static Dictionary<int, JournalDetailResponsePayload> CachedDetails { get; set; }
			= new Dictionary<int, JournalDetailResponsePayload>();

		/// <summary>
		/// Resets all playback-related state to defaults.
		/// </summary>
		public static void ResetPlaybackState()
		{
			PlaybackQueue.Clear();
			CurrentPlaybackIndex = 0;
			IsPlaying = false;
			IsPaused = false;
		}

		/// <summary>
		/// Resets all selection and playback state to defaults.
		/// </summary>
		public static void ResetAll()
		{
			SelectedJournalIds.Clear();
			CachedDetails.Clear();
			ResetPlaybackState();
			IsFloatingMode = false;
		}
	}
}
