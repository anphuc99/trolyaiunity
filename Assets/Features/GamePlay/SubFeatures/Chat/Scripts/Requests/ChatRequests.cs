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
		public const string GetCharacterPitch = "game.play.chat.get.character.pitch.request";
		public const string GetCharacterSpeakingRate = "game.play.chat.get.character.speaking.rate.request";
		public const string PlayMessageAudio = "game.play.chat.play.message.audio.request";
	}
}
