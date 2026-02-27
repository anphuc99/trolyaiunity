using System;

namespace Features.GamePlay.SubFeatures.Journal.Requests
{
	/// <summary>
	/// Request keys for this subfeature.
	/// Playback control requests have been moved to JournalOverlay feature.
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

		/// <summary>
		/// Saves selected journal IDs to GlobalVariables and loads the JournalOverlay scene.
		/// </summary>
		public const string StartPlayback = "game.play.journal.start.playback.request";
	}
}
