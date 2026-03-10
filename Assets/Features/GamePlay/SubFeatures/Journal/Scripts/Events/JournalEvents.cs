using System;

namespace Features.GamePlay.SubFeatures.Journal.Events
{
	/// <summary>
	/// Event keys for this feature.
	/// Playback events have been moved to JournalOverlay feature.
	/// </summary>
	public static class JournalEvents
	{
		public const string Echoed = "game.play.journal.echo.event";
		public const string Installed = "game.play.journal.installed.event";
		public const string Uninstalled = "game.play.journal.uninstalled.event";
		public const string JournalsLoaded = "game.play.journal.list.loaded.event";
		public const string JournalDetailLoaded = "game.play.journal.detail.loaded.event";
		public const string MessageAudioPlayRequested = "game.play.journal.message.audio.play.requested.event";
		public const string RequestFailed = "game.play.journal.request.failed.event";
		public const string ViewModeChanged = "game.play.journal.view.mode.changed.event";

		/// <summary>Due journals loaded for FSRS review.</summary>
		public const string DueJournalsLoaded = "game.play.journal.due.loaded.event";

		/// <summary>A journal FSRS review was submitted successfully.</summary>
		public const string ReviewSubmitted = "game.play.journal.review.submitted.event";

		/// <summary>Journal audio download completed or failed.</summary>
		public const string AudioDownloadCompleted = "game.play.journal.audio.download.completed.event";
	}
}
