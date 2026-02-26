using System;

namespace Features.GamePlay.Model
{
	/// <summary>
	/// Payload for requesting a menu item to be added in GamePlay view.
	/// </summary>
	public sealed class GamePlayMenuAddPayload
	{
		/// <summary>
		/// Stable identifier for the menu item.
		/// </summary>
		public string Id { get; set; }

		/// <summary>
		/// Display text shown on the menu item.
		/// </summary>
		public string Text { get; set; }

		/// <summary>
		/// Click callback invoked by the menu item.
		/// </summary>
		public Action OnClick { get; set; }
	}

	/// <summary>
	/// Payload for requesting a menu item to be removed in GamePlay view.
	/// </summary>
	public sealed class GamePlayMenuRemovePayload
	{
		/// <summary>
		/// Identifier of the menu item to remove.
		/// </summary>
		public string Id { get; set; }
	}
}