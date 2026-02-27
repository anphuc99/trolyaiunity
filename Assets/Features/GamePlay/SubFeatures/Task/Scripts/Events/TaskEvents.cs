using System;

namespace Features.GamePlay.SubFeatures.Task.Events
{
	/// <summary>
	/// Event keys for this feature.
	/// </summary>
	public static class TaskEvents
	{
		public const string Echoed = "game.play.task.echo.event";
		public const string Installed = "game.play.task.installed.event";
		public const string Uninstalled = "game.play.task.uninstalled.event";
		public const string TodayLoaded = "game.play.task.today.loaded.event";
		public const string RequestFailed = "game.play.task.request.failed.event";
	}
}
