using System;

namespace Features.GamePlay.SubFeatures.Home.Model
{
	/// <summary>
	/// Parent-provided callbacks for child-to-parent signaling.
	/// </summary>
	public sealed class HomeParentSignals
	{
		/// <summary>
		/// Optional callback invoked when the child echoes a payload.
		/// </summary>
		public Action<object> OnEchoed { get; set; }

		/// <summary>
		/// Optional callback for retrieving data from the parent.
		/// </summary>
		public Func<string> GetParentStatus { get; set; }

		public Action OpenJournal { get; set; }

		public Action OpenStory { get; set; }

		public Action OpenCreateCharacter { get; set; }
		public Action OpenCharacter { get; set; }
		public Action OpenPractice { get; set; }
		public Action OpenTask { get; set; }
	}
}
