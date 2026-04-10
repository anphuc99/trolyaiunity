using System;

namespace Features.GamePlay.SubFeatures.PracticeVocabulary.Events
{
	/// <summary>
	/// Event keys for this feature.
	/// </summary>
	public static class PracticeVocabularyEvents
	{
		public const string Echoed = "game.play.practice.vocabulary.echo.event";
		public const string Installed = "game.play.practice.vocabulary.installed.event";
		public const string Uninstalled = "game.play.practice.vocabulary.uninstalled.event";
		public const string DueReviewsLoaded = "game.play.practice.vocabulary.due.reviews.loaded.event";
		public const string ReviewSubmitted = "game.play.practice.vocabulary.review.submitted.event";
		public const string VocabularyAudioPlayRequested = "game.play.practice.vocabulary.audio.play.requested.event";
		public const string RequestFailed = "game.play.practice.vocabulary.request.failed.event";
	}
}
