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
		public const string HasAnySceneCharacter = "game.play.chat.has.any.scene.character.request";
		public const string GetAllCharacterNames = "game.play.chat.get.all.character.names.request";
		public const string GenerateReplyFromHistory = "game.play.chat.generate.reply.from.history.request";
		public const string GetCharacterAvatar = "game.play.chat.get.character.avatar.request";
		public const string GetCharacterVoiceName = "game.play.chat.get.character.voice.name.request";
		public const string GetCharacterPitch = "game.play.chat.get.character.pitch.request";
		public const string GetCharacterSpeakingRate = "game.play.chat.get.character.speaking.rate.request";
		public const string PlayMessageAudio = "game.play.chat.play.message.audio.request";
		public const string OpenAddCharacterPopup = "game.play.chat.open.add.character.popup.request";
		public const string SetCharacterActive = "game.play.chat.set.character.active.request";
		public const string LoadDeveloperState = "game.play.chat.load.developer.state.request";
		public const string SaveContext = "game.play.chat.save.context.request";
		public const string EndConversation = "game.play.chat.end.conversation.request";
		public const string TranscribeAudio = "game.play.chat.transcribe.audio.request";
		public const string LookupVocabulary = "game.play.chat.lookup.vocabulary.request";
		public const string ReviewVocabulary = "game.play.chat.review.vocabulary.request";
		public const string LoadVocabularyLearnedCount = "game.play.chat.load.vocabulary.learned.count.request";
		public const string LoadAutoChatVocabulary = "game.play.chat.load.autochat.vocabulary.request";
		public const string ResolveTurnAudio = "game.play.chat.resolve.turn.audio.request";
		public const string BatchReviewAutoChatVocabulary = "game.play.chat.batch.review.autochat.vocabulary.request";
	}
}
