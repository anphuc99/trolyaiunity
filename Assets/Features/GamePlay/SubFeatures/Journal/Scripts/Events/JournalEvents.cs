using System;

namespace Features.GamePlay.SubFeatures.Journal.Events
{
	/// <summary>
	/// Event keys for this feature.
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

		// --- Selection events ---

		/// <summary>Published when multi-select state changes.</summary>
		public const string SelectionChanged = "game.play.journal.selection.changed.event";

		// --- Playback events ---

		/// <summary>Published when sequential playback begins.</summary>
		public const string PlaybackStarted = "game.play.journal.playback.started.event";

		/// <summary>Published when playback is fully stopped.</summary>
		public const string PlaybackStopped = "game.play.journal.playback.stopped.event";

		/// <summary>Published when playback is paused.</summary>
		public const string PlaybackPaused = "game.play.journal.playback.paused.event";

		/// <summary>Published when playback resumes from pause.</summary>
		public const string PlaybackResumed = "game.play.journal.playback.resumed.event";

		/// <summary>Published when the current playback message changes (includes audio URL).</summary>
		public const string PlaybackMessageChanged = "game.play.journal.playback.message.changed.event";

		// --- Floating overlay events ---

		/// <summary>Published when floating overlay mode is toggled.</summary>
		public const string FloatingModeChanged = "game.play.journal.floating.mode.changed.event";
	}
}
