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
		public const string HistoryLoaded = "game.play.chat.history.loaded.event";
		public const string MessageReceived = "game.play.chat.message.received.event";
		public const string MessageAudioPlayRequested = "game.play.chat.message.audio.play.requested.event";
		public const string CharactersLoaded = "game.play.chat.characters.loaded.event";
		public const string ContextInputRequested = "game.play.chat.context.input.requested.event";
		public const string AutoChatToggleRequested = "game.play.chat.auto.chat.toggle.requested.event";
		public const string DeveloperStateLoaded = "game.play.chat.developer.state.loaded.event";
		public const string EndConversationRequested = "game.play.chat.end.conversation.requested.event";
		public const string ConversationEnded = "game.play.chat.conversation.ended.event";
		public const string RequestFailed = "game.play.chat.request.failed.event";
		public const string TranscriptionCompleted = "game.play.chat.transcription.completed.event";
		public const string AudioRecordingTranscribed = "game.play.chat.audio.recording.transcribed.event";
		public const string VocabLookupCompleted = "game.play.chat.vocab.lookup.completed.event";
		public const string VocabReviewCompleted = "game.play.chat.vocab.review.completed.event";
		public const string VocabularyLearnedCountLoaded = "game.play.chat.vocabulary.learned.count.loaded.event";
		public const string MessageContentUpdated = "game.play.chat.message.content.updated.event";
		public const string AutoChatVocabularyLoaded = "game.play.chat.autochat.vocabulary.loaded.event";
		public const string VocabExampleGenerated = "game.play.chat.vocab.example.generated.event";
		public const string MissionVocabularyLoaded = "game.play.chat.mission.vocabulary.loaded.event";
	}
}
