using System;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Journal.Model
{
	/// <summary>
	/// Parent-provided callbacks for child-to-parent signaling.
	/// </summary>
	public sealed class JournalParentSignals
	{
		/// <summary>
		/// Optional callback invoked when the child echoes a payload.
		/// </summary>
		public Action<object> OnEchoed { get; set; }

		/// <summary>
		/// Optional callback for retrieving data from the parent.
		/// </summary>
		public Func<string> GetParentStatus { get; set; }

		public Func<string, Sprite> GetAvatar { get; set; }
	}
}
