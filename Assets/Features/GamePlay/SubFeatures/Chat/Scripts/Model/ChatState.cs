using System.Collections.Generic;

namespace Features.GamePlay.SubFeatures.Chat.Model
{
	/// <summary>
	/// Holds subfeature state, including parent signal bindings.
	/// </summary>
	public static class ChatState
	{
		public static ChatParentSignals ParentSignals { get; set; }

		public static string AddCharacterMenuId { get; set; }

		public static string ContextMenuId { get; set; }

		public static string EndConversationMenuId { get; set; }

		/// <summary>
		/// Vocabulary candidates loaded from learning paths.
		/// </summary>
		public static List<string> LearningPathVocabularyCandidates { get; set; } = new List<string>();

		/// <summary>
		/// Learned/saved vocabulary words loaded from user vocabularies.
		/// </summary>
		public static HashSet<string> LearnedVocabularySet { get; set; } = new HashSet<string>(System.StringComparer.Ordinal);

		/// <summary>
		/// True when vocabulary marker sources were loaded successfully at least once.
		/// </summary>
		public static bool IsVocabularyMarkerSourceLoaded { get; set; }
	}
}
