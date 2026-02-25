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
		/// Character display name.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Character avatar sprite.
		/// </summary>
		public Sprite Avatar { get; set; }

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
	}
}
