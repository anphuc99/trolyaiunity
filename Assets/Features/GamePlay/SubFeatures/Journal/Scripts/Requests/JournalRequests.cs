using System;

namespace Features.GamePlay.SubFeatures.Journal.Requests
{
	/// <summary>
	/// Request keys for this subfeature.
	/// </summary>
	public static class JournalRequests
	{
		public const string Echo = "game.play.journal.echo.request";
		public const string LoadJournals = "game.play.journal.load.list.request";
		public const string LoadJournalDetail = "game.play.journal.load.detail.request";
		public const string ShowJournalList = "game.play.journal.show.list.request";
		public const string PlayMessageAudio = "game.play.journal.play.message.audio.request";
	}
}
