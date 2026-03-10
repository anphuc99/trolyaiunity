namespace Core.Infrastructure.State
{
	/// <summary>
	/// GlobalVariables keys and values used to switch feature API modes.
	/// </summary>
	public static class GlobalModes
	{
		/// <summary>
		/// Key for selecting chat API mode.
		/// </summary>
		public const string ChatApiModeKey = "global.gameplay.chat.api.mode";

		/// <summary>
		/// Key for selecting journal API mode.
		/// </summary>
		public const string JournalApiModeKey = "global.gameplay.journal.api.mode";

		/// <summary>
		/// Key for storing the selected knowledge ID for learning.
		/// </summary>
		public const string KnowledgeIdKey = "global.gameplay.knowledge.id";

		/// <summary>
		/// Default API mode value (practice mode from gameplay).
		/// </summary>
		public const string ModeDefault = "default";

		/// <summary>
		/// MyLog API mode value.
		/// </summary>
		public const string ModeMyLog = "mylog";

		/// <summary>
		/// Learn mode value (from knowledges).
		/// </summary>
		public const string ModeLearn = "learn";
	}
}