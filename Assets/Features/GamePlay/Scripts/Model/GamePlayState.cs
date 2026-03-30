namespace Features.GamePlay.Model
{
	/// <summary>
	/// Cached character data for chat usage in GamePlay scope.
	/// </summary>
	public sealed class GamePlayChatCharacterCache
	{
		public int Id { get; set; }
		public string Name { get; set; }
		public string Personality { get; set; }
		public string Gender { get; set; }
		public int? Age { get; set; }
		public string Appearance { get; set; }
		public string AvatarUrl { get; set; }
		public string VoiceModel { get; set; }
		public string VoiceName { get; set; }
		public float? Pitch { get; set; }
		public float? SpeakingRate { get; set; }
		public string CreatedAt { get; set; }
		public string UpdatedAt { get; set; }
		public UnityEngine.Sprite AvatarSprite { get; set; }
	}

	/// <summary>
	/// Holds GamePlay runtime state.
	/// </summary>
	public static class GamePlayState
	{
		public static GamePlaySubControllerType? CurrentSubController { get; set; }

		public static readonly System.Collections.Generic.Dictionary<string, GamePlayChatCharacterCache> ChatCharacterByName =
			new System.Collections.Generic.Dictionary<string, GamePlayChatCharacterCache>(System.StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Character relationships keyed by ownerCharacterId.
		/// Populated during scope entry from GET /api/character-relationships.
		/// </summary>
		public static readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<Shares.Model.CharacterRelationshipInfo>> RelationshipsByCharacterId =
			new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<Shares.Model.CharacterRelationshipInfo>>();
	}
}
