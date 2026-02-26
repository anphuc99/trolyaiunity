using System;

namespace Features.GamePlay.SubFeatures.Practice.Events
{
	/// <summary>
	/// Event keys for this feature.
	/// </summary>
	public static class PracticeEvents
	{
		public const string Echoed = "game.play.practice.echo.event";
		public const string Installed = "game.play.practice.installed.event";
		public const string Uninstalled = "game.play.practice.uninstalled.event";
		public const string TabLoaded = "game.play.practice.tab.loaded.event";
		public const string ContextLoaded = "game.play.practice.context.loaded.event";
		public const string ReviewSubmitted = "game.play.practice.review.submitted.event";
		public const string RequestFailed = "game.play.practice.request.failed.event";
	}
}
