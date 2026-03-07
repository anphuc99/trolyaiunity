using System;

namespace Features.GamePlay.Events
{
	/// <summary>
	/// Event keys for this feature.
	/// </summary>
	public static class GamePlayEvents
	{
		public const string Echoed = "game.play.echo.event";
		public const string SubControllerChanged = "game.play.subcontroller.changed.event";
		public const string MenuItemAddRequested = "game.play.menu.item.add.requested.event";
		public const string MenuItemRemoveRequested = "game.play.menu.item.remove.requested.event";
	}
}
