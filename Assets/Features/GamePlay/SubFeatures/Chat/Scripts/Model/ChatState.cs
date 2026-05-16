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

		public static string AutoChatMenuId { get; set; }

		public static string EndConversationMenuId { get; set; }

		/// <summary>
		/// Active character names in the current chat scene, synchronized from developer state.
		/// </summary>
		public static HashSet<string> ActiveCharacterNames { get; set; } = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
	}
}
