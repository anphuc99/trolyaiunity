namespace Core.Infrastructure.Network
{
	/// <summary>
	/// Contains all network API endpoint paths.
	/// </summary>
	public static class NetworkEndpoints
	{
		public const string Health = "/api/health";
		public const string Version = "/api/version";
		public const string Login = "/api/auth/login";
		public const string TokenValidate = "/api/auth/token/validate";
		public const string TokenRefresh = "/api/auth/token/refresh";
		public const string Characters = "/api/characters";
		public const string CharactersUploadAvatar = "/api/characters/upload-avatar";
		public const string Levels = "/api/levels";
		public const string ChatHistory = "/api/chat/history";
		public const string ChatSend = "/api/chat/send";
		public const string ChatRespond = "/api/chat/respond";
		public const string ChatTranscribe = "/api/chat/transcribe";
		public const string ChatDeveloperState = "/api/chat/developer-state";
		public const string ChatDeveloper = "/api/chat/developer";
		public const string Journals = "/api/journals";
		public const string JournalsEnd = "/api/journals/end";
		public const string JournalReviewDue = "/api/journals/review/due";
		public const string JournalReview = "/api/journals/review";
		public const string Stories = "/api/stories";
		public const string MyLog = "/api/mylog";
		public const string MyLogChatHistory = "/api/mylog/chat/history";
		public const string MyLogChatSend = "/api/mylog/chat/send";
		public const string MyLogChatEnd = "/api/mylog/chat/end";
		public const string MyLogChatDeveloperState = "/api/mylog/chat/developer-state";
		public const string MyLogChatDeveloper = "/api/mylog/chat/developer";
		public const string MyLogChatEdit = "/api/mylog/chat/edit";
		public const string MyLogJournals = "/api/mylog/journals";
		public const string MyLogJournalReviewDue = "/api/mylog/review/due";
		public const string MyLogJournalReview = "/api/mylog/review";
		public const string Translation = "/api/translation";
		public const string TranslationDue = "/api/translation/due";
		public const string TranslationLearn = "/api/translation/learn";
		public const string TranslationContext = "/api/translation/context";
		public const string TranslationReview = "/api/translation/review";
		public const string TasksToday = "/api/tasks/today";
		public const string TextToSpeech = "/api/text-to-speech";
		public const string UserMe = "/api/users/me";
		public const string UserProfile = "/api/users/profile";
		public const string Personalities = "/personalities";
	}
}
