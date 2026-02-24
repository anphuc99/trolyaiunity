using System;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Chat.Model
{
	/// <summary>
	/// Parent-provided callbacks for child-to-parent signaling.
	/// </summary>
	public sealed class ChatParentSignals
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
		/// Optional callback for getting cached character avatar by name.
		/// </summary>
		public Func<string, Sprite> GetCharacterAvatarByName { get; set; }

		/// <summary>
		/// Optional callback for getting cached character voice name by character name.
		/// </summary>
		public Func<string, string> GetCharacterVoiceNameByName { get; set; }

		/// <summary>
		/// Optional callback for getting cached character pitch by character name.
		/// </summary>
		public Func<string, float?> GetCharacterPitchByName { get; set; }

		/// <summary>
		/// Optional callback for getting cached character speaking rate by character name.
		/// </summary>
		public Func<string, float?> GetCharacterSpeakingRateByName { get; set; }
	}
}
