using System.Collections.Generic;

namespace Features.JournalOverlay.Model
{
	/// <summary>
	/// Static state for the JournalOverlay feature.
	/// Stores playback queue, current index, and playback flags.
	/// </summary>
	public static class JournalOverlayState
	{
		/// <summary>
		/// Journal IDs selected for playback (copied from GlobalVariables on scope enter).
		/// </summary>
		public static List<int> SelectedJournalIds { get; set; } = new List<int>();

		/// <summary>
		/// Flattened playlist of messages from all selected journals.
		/// </summary>
		public static List<OverlayPlaybackQueueItem> PlaybackQueue { get; set; } = new List<OverlayPlaybackQueueItem>();

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
		/// Cached journal detail responses keyed by journal id.
		/// </summary>
		public static Dictionary<int, OverlayJournalDetailResponse> CachedDetails { get; set; }
			= new Dictionary<int, OverlayJournalDetailResponse>();

		/// <summary>
		/// Resets playback-related state to defaults.
		/// </summary>
		public static void ResetPlayback()
		{
			PlaybackQueue.Clear();
			CurrentPlaybackIndex = 0;
			IsPlaying = false;
			IsPaused = false;
		}

		/// <summary>
		/// Resets all state to defaults.
		/// </summary>
		public static void ResetAll()
		{
			SelectedJournalIds.Clear();
			CachedDetails.Clear();
			ResetPlayback();
		}
	}
}
