using System;
using UnityEngine;

namespace Share.Model
{
	/// <summary>
	/// Shared selected-character payload model used to pass character data across controller scopes.
	/// </summary>
	[Serializable]
	public sealed class SelectedCharacterInfo
	{
		/// <summary>
		/// Character identifier.
		/// </summary>
		public int Id { get; set; }

		/// <summary>
		/// Character display name.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Character avatar sprite.
		/// </summary>
		public Sprite Avatar { get; set; }

		/// <summary>
		/// Character avatar URL.
		/// </summary>
		public string AvatarUrl { get; set; }

		/// <summary>
		/// Character age when available.
		/// </summary>
		public int? Age { get; set; }

		/// <summary>
		/// Character description/personality text.
		/// </summary>
		public string Description { get; set; }

		/// <summary>
		/// Character gender text.
		/// </summary>
		public string Gender { get; set; }

		/// <summary>
		/// Character voice name.
		/// </summary>
		public string VoiceName { get; set; }

		/// <summary>
		/// Character voice pitch.
		/// </summary>
		public float? Pitch { get; set; }

		/// <summary>
		/// Character speaking rate.
		/// </summary>
		public float? SpeakingRate { get; set; }
	}

	/// <summary>
	/// Global notice payload for a recently deleted character.
	/// </summary>
	public sealed class DeletedCharacterNotice
	{
		/// <summary>
		/// Deleted character identifier.
		/// </summary>
		public int CharacterId { get; set; }

		/// <summary>
		/// Deleted character display name.
		/// </summary>
		public string CharacterName { get; set; }
	}

	/// <summary>
	/// Global notice payload for a recently edited character.
	/// </summary>
	public sealed class EditedCharacterNotice
	{
		/// <summary>
		/// Updated character payload.
		/// </summary>
		public SelectedCharacterInfo Character { get; set; }

		/// <summary>
		/// Updated avatar URL from server.
		/// </summary>
		public string AvatarUrl { get; set; }

		/// <summary>
		/// Updated speaking rate.
		/// </summary>
		public float? SpeakingRate { get; set; }
	}
}
