using System;

namespace Features.JournalOverlay.Events
{
	/// <summary>
	/// Event keys for the JournalOverlay feature.
	/// </summary>
	public static class JournalOverlayEvents
	{
		public const string Echoed = "journal.overlay.echo.event";

		/// <summary>Published when playback queue is built and playback begins.</summary>
		public const string PlaybackStarted = "journal.overlay.playback.started.event";

		/// <summary>Published when playback is fully stopped.</summary>
		public const string PlaybackStopped = "journal.overlay.playback.stopped.event";

		/// <summary>Published when playback is paused.</summary>
		public const string PlaybackPaused = "journal.overlay.playback.paused.event";

		/// <summary>Published when playback resumes from pause.</summary>
		public const string PlaybackResumed = "journal.overlay.playback.resumed.event";

		/// <summary>Published when the current playback message changes (with audio URL).</summary>
		public const string PlaybackMessageChanged = "journal.overlay.playback.message.changed.event";

		/// <summary>Published when journal details are being loaded (loading indicator).</summary>
		public const string Loading = "journal.overlay.loading.event";

		/// <summary>Published when an error occurs.</summary>
		public const string RequestFailed = "journal.overlay.request.failed.event";

		/// <summary>Published to signal that the overlay should close and return to previous scene.</summary>
		public const string CloseRequested = "journal.overlay.close.requested.event";
	}
}
