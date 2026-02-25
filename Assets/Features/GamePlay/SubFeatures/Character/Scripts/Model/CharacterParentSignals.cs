using System;
using System.Collections.Generic;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Character.Model
{
	/// <summary>
	/// Parent-provided callbacks for child-to-parent signaling.
	/// </summary>
	public sealed class CharacterParentSignals
	{
		/// <summary>
		/// Optional callback invoked when the child echoes a payload.
		/// </summary>
		public Action<object> OnEchoed { get; set; }

		/// <summary>
		/// Optional callback for retrieving data from the parent.
		/// </summary>
		public Func<string> GetParentStatus { get; set; }

		/// <summary>
		/// Provides character names from parent cache.
		/// </summary>
		public Func<List<string>> GetCharacterNames { get; set; }

		/// <summary>
		/// Provides avatar sprite by character name.
		/// </summary>
		public Func<string, Sprite> GetCharacterAvatar { get; set; }
	}
}
