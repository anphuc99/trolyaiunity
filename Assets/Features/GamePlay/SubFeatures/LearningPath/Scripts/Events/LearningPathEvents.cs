using System;

namespace Features.GamePlay.SubFeatures.LearningPath.Events
{
	/// <summary>
	/// Event keys for this feature.
	/// </summary>
	public static class LearningPathEvents
	{
		public const string Echoed = "game.play.learning.path.echo.event";
		public const string Installed = "game.play.learning.path.installed.event";
		public const string Uninstalled = "game.play.learning.path.uninstalled.event";
		public const string ListLoaded = "game.play.learning.path.list.loaded.event";
		public const string EditLoaded = "game.play.learning.path.edit.loaded.event";
		public const string Saved = "game.play.learning.path.saved.event";
		public const string RequestFailed = "game.play.learning.path.request.failed.event";
	}
}
