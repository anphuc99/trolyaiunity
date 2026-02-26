namespace Features.GamePlay.SubFeatures.Chat.Model
{
	/// <summary>
	/// Holds subfeature state, including parent signal bindings.
	/// </summary>
	public static class ChatState
	{
		public static ChatParentSignals ParentSignals { get; set; }

		public static string AddCharacterMenuId { get; set; }
	}
}
