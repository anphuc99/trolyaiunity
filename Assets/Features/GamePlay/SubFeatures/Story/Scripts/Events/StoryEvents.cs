using System;

namespace Features.GamePlay.SubFeatures.Story.Events
{
	/// <summary>
	/// Event keys for this feature.
	/// </summary>
	public static class StoryEvents
	{
		public const string Echoed = "game.play.story.echo.event";
		public const string Installed = "game.play.story.installed.event";
		public const string Uninstalled = "game.play.story.uninstalled.event";
		public const string StoriesLoaded = "game.play.story.list.loaded.event";
		public const string StoryEditLoaded = "game.play.story.edit.loaded.event";
		public const string StorySaved = "game.play.story.saved.event";
		public const string RequestFailed = "game.play.story.request.failed.event";
	}
}
