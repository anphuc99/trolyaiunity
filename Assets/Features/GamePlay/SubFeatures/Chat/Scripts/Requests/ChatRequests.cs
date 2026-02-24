using System;

namespace Features.GamePlay.SubFeatures.Chat.Requests
{
	/// <summary>
	/// Request keys for this subfeature.
	/// </summary>
	public static class ChatRequests
	{
		public const string Echo = "game.play.chat.echo.request";
		public const string LoadHistory = "game.play.chat.load.history.request";
		public const string SendMessage = "game.play.chat.send.message.request";
		public const string GetCharacterAvatar = "game.play.chat.get.character.avatar.request";
		public const string GetCharacterVoiceName = "game.play.chat.get.character.voice.name.request";
	}
}
