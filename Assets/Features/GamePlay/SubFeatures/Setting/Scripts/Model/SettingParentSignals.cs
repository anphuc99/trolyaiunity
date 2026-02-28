using System;
using System.Collections.Generic;

namespace Features.GamePlay.SubFeatures.Setting.Model
{
	/// <summary>
	/// Parent-provided callbacks for child-to-parent signaling.
	/// </summary>
	public sealed class SettingParentSignals
	{
		/// <summary>
		/// Optional callback invoked when the child echoes a payload (reserved for future use).
		/// </summary>
		public Action<object> OnEchoed { get; set; }

		/// <summary>
		/// Retrieves the list of cached character names used for voice configuration.
		/// </summary>
		public Func<IReadOnlyList<string>> GetCharacterNames { get; set; }

		/// <summary>
		/// Resolves a character voice name by character name.
		/// </summary>
		public Func<string, string> GetCharacterVoiceName { get; set; }

		/// <summary>
		/// Resolves a character pitch by character name.
		/// </summary>
		public Func<string, float?> GetCharacterPitch { get; set; }
	}
}
