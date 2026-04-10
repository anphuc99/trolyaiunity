using System;
using System.Collections.Generic;

namespace Features.GamePlay.SubFeatures.PracticeVocabulary.Model
{
	/// <summary>
	/// Parent-provided callbacks for child-to-parent signaling.
	/// </summary>
	public sealed class PracticeVocabularyParentSignals
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
		/// Optional callback for getting all cached character names from parent scope.
		/// </summary>
		public Func<List<string>> GetCharacterNames { get; set; }

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
