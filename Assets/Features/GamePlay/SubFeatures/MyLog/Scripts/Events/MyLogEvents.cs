using System;

namespace Features.GamePlay.SubFeatures.MyLog.Events
{
	/// <summary>
	/// Event keys for this feature.
	/// </summary>
	public static class MyLogEvents
	{
		public const string Echoed = "game.play.my.log.echo.event";
		public const string Installed = "game.play.my.log.installed.event";
		public const string Uninstalled = "game.play.my.log.uninstalled.event";
		public const string LogsLoaded = "game.play.my.log.list.loaded.event";
		public const string LogEditLoaded = "game.play.my.log.edit.loaded.event";
		public const string LogSaved = "game.play.my.log.saved.event";
		public const string RequestFailed = "game.play.my.log.request.failed.event";
	}
}
