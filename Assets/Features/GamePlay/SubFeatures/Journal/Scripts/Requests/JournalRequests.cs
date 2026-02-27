using System;

namespace Features.GamePlay.SubFeatures.Journal.Requests
{
	/// <summary>
	/// Request keys for this subfeature.
	/// </summary>
	public static class JournalRequests
	{
		public const string Echo = "game.play.journal.echo.request";
		public const string LoadJournals = "game.play.journal.load.list.request";
		public const string LoadJournalDetail = "game.play.journal.load.detail.request";
		public const string ShowJournalList = "game.play.journal.show.list.request";
		public const string PlayMessageAudio = "game.play.journal.play.message.audio.request";

		// --- Selection requests ---

		/// <summary>Toggles one journal in/out of the multi-select set.</summary>
		public const string ToggleJournalSelection = "game.play.journal.toggle.selection.request";

		// --- Playback requests ---

		/// <summary>Starts sequential playback of all selected journals.</summary>
		public const string StartPlayback = "game.play.journal.start.playback.request";

		/// <summary>Pauses current playback.</summary>
		public const string PausePlayback = "game.play.journal.pause.playback.request";

		/// <summary>Resumes paused playback.</summary>
		public const string ResumePlayback = "game.play.journal.resume.playback.request";

		/// <summary>Skips to next message in the queue.</summary>
		public const string NextMessage = "game.play.journal.next.message.request";

		/// <summary>Goes back to previous message in the queue.</summary>
		public const string PreviousMessage = "game.play.journal.previous.message.request";

		/// <summary>Stops playback completely and resets state.</summary>
		public const string StopPlayback = "game.play.journal.stop.playback.request";

		/// <summary>Toggles floating overlay mode.</summary>
		public const string ToggleFloatingMode = "game.play.journal.toggle.floating.request";

		/// <summary>Internal: View calls this when current audio finishes to advance the queue.</summary>
		public const string AdvancePlayback = "game.play.journal.advance.playback.request";
	}
}
