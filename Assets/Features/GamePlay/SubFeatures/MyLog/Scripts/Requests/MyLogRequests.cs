using System;

namespace Features.GamePlay.SubFeatures.MyLog.Requests
{
	/// <summary>
	/// Request keys for this subfeature.
	/// </summary>
	public static class MyLogRequests
	{
		public const string Echo = "game.play.my.log.echo.request";
		public const string LoadLogs = "game.play.my.log.load.list.request";
		public const string CreateLog = "game.play.my.log.create.request";
		public const string UpdateLog = "game.play.my.log.update.request";
		public const string EditLog = "game.play.my.log.edit.request";
	}
}
