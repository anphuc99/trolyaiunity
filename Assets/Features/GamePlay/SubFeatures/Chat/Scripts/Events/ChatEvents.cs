using System;

namespace Features.GamePlay.SubFeatures.Chat.Events
{
	/// <summary>
	/// Event keys for this feature.
	/// </summary>
	public static class ChatEvents
	{
		public const string Echoed = "game.play.chat.echo.event";
		public const string Installed = "game.play.chat.installed.event";
		public const string Uninstalled = "game.play.chat.uninstalled.event";
	}
}
