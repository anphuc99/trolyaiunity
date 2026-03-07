using System;

namespace Features.GamePlay.Requests
{
	/// <summary>
	/// Request keys for this feature.
	/// </summary>
	public static class GamePlayRequests
	{
		public const string Echo = "game.play.echo.request";
		public const string OpenSubController = "game.play.open.subcontroller.request";
		public const string CloseCurrentSubController = "game.play.close.current.subcontroller.request";
		public const string GetCurrentSubController = "game.play.get.current.subcontroller.request";

		/// <summary>
		/// Opens regular (non-MyLog) Chat, resetting API mode to default.
		/// </summary>
		public const string OpenChat = "game.play.open.chat.request";
	}
}
