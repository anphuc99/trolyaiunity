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
	}
}
