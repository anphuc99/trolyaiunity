using System;

namespace Features.GamePlay.SubFeatures.Setting.Events
{
	/// <summary>
	/// Event keys for the Setting subfeature lifecycle and data flow.
	/// </summary>
	public static class SettingEvents
	{
		public const string Installed = "game.play.setting.installed.event";
		public const string Uninstalled = "game.play.setting.uninstalled.event";
		public const string ProfileLoadStarted = "game.play.setting.profile.load.started.event";
		public const string ProfileLoaded = "game.play.setting.profile.loaded.event";
		public const string ProfileLoadFailed = "game.play.setting.profile.load.failed.event";
		public const string ProfileSaveStarted = "game.play.setting.profile.save.started.event";
		public const string ProfileSaveSucceeded = "game.play.setting.profile.save.succeeded.event";
		public const string ProfileSaveFailed = "game.play.setting.profile.save.failed.event";
	}
}
