using System;

namespace Features.JournalOverlay.Requests
{
	/// <summary>
	/// Request keys for the JournalOverlay feature.
	/// </summary>
	public static class JournalOverlayRequests
	{
		public const string Echo = "journal.overlay.echo.request";

		/// <summary>Starts loading journal details and building the playback queue.</summary>
		public const string StartPlayback = "journal.overlay.start.playback.request";

		/// <summary>Pauses current playback.</summary>
		public const string PausePlayback = "journal.overlay.pause.playback.request";

		/// <summary>Resumes paused playback.</summary>
		public const string ResumePlayback = "journal.overlay.resume.playback.request";

		/// <summary>Skips to next message in the queue.</summary>
		public const string NextMessage = "journal.overlay.next.message.request";

		/// <summary>Goes back to previous message in the queue.</summary>
		public const string PreviousMessage = "journal.overlay.previous.message.request";

		/// <summary>Stops playback completely and resets state.</summary>
		public const string StopPlayback = "journal.overlay.stop.playback.request";

		/// <summary>Internal: View calls this when audio finishes to advance the queue.</summary>
		public const string AdvancePlayback = "journal.overlay.advance.playback.request";

		/// <summary>Closes the overlay, restores the window, and returns to the previous scene.</summary>
		public const string Close = "journal.overlay.close.request";
	}
}
