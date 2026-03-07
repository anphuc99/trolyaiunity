namespace Features.GamePlay.SubFeatures.Character.Model
{
	/// <summary>
	/// Holds subfeature state, including parent signal bindings.
	/// </summary>
	public static class CharacterState
	{
		public static CharacterParentSignals ParentSignals { get; set; }

		public static System.Collections.Generic.List<CharacterListItemPayload> CachedCharacters { get; set; } =
			new System.Collections.Generic.List<CharacterListItemPayload>();
	}
}
