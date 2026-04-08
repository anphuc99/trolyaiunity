using System;

namespace Features.GamePlay.SubFeatures.Journal.Requests
{
	/// <summary>
	/// Request keys for this subfeature.
	/// Playback control requests have been moved to JournalOverlay feature.
	/// </summary>
	public static class JournalRequests
	{
		public const string Echo = "game.play.journal.echo.request";
		public const string LoadJournals = "game.play.journal.load.list.request";
		public const string LoadJournalDetail = "game.play.journal.load.detail.request";
		public const string ShowJournalList = "game.play.journal.show.list.request";
		public const string PlayMessageAudio = "game.play.journal.play.message.audio.request";
		public const string GetAllCharacterNames = "game.play.journal.get.all.character.names.request";
		public const string LookupVocabulary = "game.play.journal.lookup.vocabulary.request";

		/// <summary>
		/// Saves selected journal IDs to GlobalVariables and loads the JournalOverlay scene.
		/// </summary>
		public const string StartPlayback = "game.play.journal.start.playback.request";

		/// <summary>
		/// Loads journals due for FSRS spaced-repetition review.
		/// </summary>
		public const string LoadDueJournals = "game.play.journal.load.due.request";

		/// <summary>
		/// Submits an FSRS review rating for a journal.
		/// </summary>
		public const string SubmitJournalReview = "game.play.journal.submit.review.request";

		/// <summary>
		/// Downloads all audio for a journal as a single combined MP3.
		/// </summary>
		public const string DownloadAudio = "game.play.journal.download.audio.request";
	}
}
