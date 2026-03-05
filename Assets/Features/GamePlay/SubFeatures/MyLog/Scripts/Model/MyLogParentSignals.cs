using System;

namespace Features.GamePlay.SubFeatures.MyLog.Model
{
	/// <summary>
	/// Parent-provided callbacks for child-to-parent signaling.
	/// </summary>
	public sealed class MyLogParentSignals
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
		/// Optional callback for adding a menu item at parent scope.
		/// </summary>
		public Func<string, Action, string> AddMenu { get; set; }

		/// <summary>
		/// Optional callback for removing a parent-scope menu item by id.
		/// </summary>
		public Action<string> RemoveMenu { get; set; }

		/// <summary>
		/// Optional callback for opening chat subfeature from MyLog.
		/// </summary>
		public Action OpenChat { get; set; }

		/// <summary>
		/// Optional callback for opening journal subfeature from MyLog.
		/// </summary>
		public Action OpenJournal { get; set; }
	}
}
