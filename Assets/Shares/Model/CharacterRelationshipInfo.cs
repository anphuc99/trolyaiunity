using Newtonsoft.Json;

namespace Shares.Model
{
	/// <summary>
	/// Represents a character-to-user or character-to-character relationship
	/// fetched from the server's character_relationships table.
	/// </summary>
	public sealed class CharacterRelationshipInfo
	{
		/// <summary>Record id.</summary>
		[JsonProperty("id")]
		public int Id { get; set; }

		/// <summary>Character who owns/feels this relationship.</summary>
		[JsonProperty("ownerCharacterId")]
		public int OwnerCharacterId { get; set; }

		/// <summary>"user" or "character".</summary>
		[JsonProperty("targetType")]
		public string TargetType { get; set; }

		/// <summary>Target character id (null when targetType is "user").</summary>
		[JsonProperty("targetCharacterId")]
		public int? TargetCharacterId { get; set; }

		/// <summary>Kind of relationship (e.g. "younger_sister", "friend").</summary>
		[JsonProperty("relationshipKind")]
		public string RelationshipKind { get; set; }

		/// <summary>Core belief in first-person voice.</summary>
		[JsonProperty("stableThought")]
		public string StableThought { get; set; }

		/// <summary>Situational reaction (may be null).</summary>
		[JsonProperty("temporaryThought")]
		public string TemporaryThought { get; set; }

		/// <summary>Deep long-term bond (0-10).</summary>
		[JsonProperty("stableEmotion")]
		public float StableEmotion { get; set; }

		/// <summary>Current volatile feeling (0-10).</summary>
		[JsonProperty("currentEmotion")]
		public float CurrentEmotion { get; set; }

		/// <summary>Reason for current emotion (may be null).</summary>
		[JsonProperty("emotionCause")]
		public string EmotionCause { get; set; }
	}
}
