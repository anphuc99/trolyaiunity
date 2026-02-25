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
		public const string RequestFailed = "game.play.journal.request.failed.event";
		public const string ViewModeChanged = "game.play.journal.view.mode.changed.event";
	}
}
