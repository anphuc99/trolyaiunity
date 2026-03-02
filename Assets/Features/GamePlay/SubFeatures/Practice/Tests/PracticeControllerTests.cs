using Core.Infrastructure.Events;
using Core.Infrastructure.Network;
using Features.GamePlay.SubFeatures.Practice.Controller;
using Features.GamePlay.SubFeatures.Practice.Events;
using Features.GamePlay.SubFeatures.Practice.Model;
using NUnit.Framework;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine.TestTools;

namespace Features.GamePlay.SubFeatures.Practice.Tests
{
	/// <summary>
	/// Basic tests for Practice controller.
	/// </summary>
	public sealed class PracticeControllerTests
	{
		[SetUp]
		public void SetUp()
		{
			EventBus.ClearAll();
		}

		[TearDown]
		public void TearDown()
		{
			EventBus.ClearAll();
			FakeServer.ResetToDefaults();
		}

		[Test]
		public void LoadTab_ShouldPublishError_WhenPayloadMissing()
		{
			PracticeErrorPayload errorPayload = null;
			EventBus.Subscribe(PracticeEvents.RequestFailed, payload =>
			{
				errorPayload = payload as PracticeErrorPayload;
			});

			PracticeController.HandleLoadTab(null);

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Missing practice tab request payload.", errorPayload.Message);
		}

		[Test]
		public void LoadContext_ShouldPublishError_WhenMessageIdMissing()
		{
			PracticeErrorPayload errorPayload = null;
			EventBus.Subscribe(PracticeEvents.RequestFailed, payload =>
			{
				errorPayload = payload as PracticeErrorPayload;
			});

			PracticeController.HandleLoadContext(new PracticeContextRequestPayload
			{
				MessageId = " "
			});

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Message id is required to load context.", errorPayload.Message);
		}

		[Test]
		public void SubmitReview_ShouldPublishError_WhenRatingInvalid()
		{
			PracticeErrorPayload errorPayload = null;
			EventBus.Subscribe(PracticeEvents.RequestFailed, payload =>
			{
				errorPayload = payload as PracticeErrorPayload;
			});

			PracticeController.HandleSubmitReview(new PracticeReviewRequestPayload
			{
				Rating = 0,
				MessageId = "msg"
			});

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Rating must be between 1 and 4.", errorPayload.Message);
		}

		[Test]
		public void PlayAudio_ShouldPublishError_WhenPayloadIsNull()
		{
			PracticeErrorPayload errorPayload = null;
			EventBus.Subscribe(PracticeEvents.RequestFailed, payload =>
			{
				errorPayload = payload as PracticeErrorPayload;
			});

			PracticeController.HandlePlayAudio(null);

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Text is required for playback.", errorPayload.Message);
		}

		[Test]
		public void PlayAudio_ShouldPublishError_WhenTextIsEmpty()
		{
			PracticeErrorPayload errorPayload = null;
			EventBus.Subscribe(PracticeEvents.RequestFailed, payload =>
			{
				errorPayload = payload as PracticeErrorPayload;
			});

			PracticeController.HandlePlayAudio(new PracticeAudioRequestPayload
			{
				Text = "  "
			});

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Text is required for playback.", errorPayload.Message);
		}

		[UnityTest]
		public IEnumerator PlayAudio_ShouldPublishAudioUrlResolved_WhenPayloadIsValid()
		{
			PracticeAudioUrlPayload audioPayload = null;
			EventBus.Subscribe(PracticeEvents.AudioUrlResolved, payload =>
			{
				audioPayload = payload as PracticeAudioUrlPayload;
			});

			FakeServer.Register("GET", NetworkEndpoints.TextToSpeech, _ =>
				"{\"url\":\"/audio/generated.mp3\"}"
			);

			PracticeController.HandlePlayAudio(new PracticeAudioRequestPayload
			{
				Text = "Xin chao",
				Tone = "neutral",
				CharacterName = "Mimi"
			});
			yield return AwaitTask(Task.Delay(100));

			Assert.IsNotNull(audioPayload);
			Assert.IsTrue(audioPayload.Url.EndsWith("/audio/generated.mp3"));
		}

		[UnityTest]
		public IEnumerator PlayAudio_ShouldUseDefaultToneAndCharacter_WhenMissing()
		{
			PracticeAudioUrlPayload audioPayload = null;
			string requestedPath = null;
			EventBus.Subscribe(PracticeEvents.AudioUrlResolved, payload =>
			{
				audioPayload = payload as PracticeAudioUrlPayload;
			});

			FakeServer.Register("GET", NetworkEndpoints.TextToSpeech, path =>
			{
				requestedPath = path;
				return "{\"url\":\"/audio/default.mp3\"}";
			});

			PracticeController.HandlePlayAudio(new PracticeAudioRequestPayload
			{
				Text = "  hello  "
			});
			yield return AwaitTask(Task.Delay(100));

			Assert.IsNotNull(audioPayload);
			Assert.IsTrue(audioPayload.Url.EndsWith("/audio/default.mp3"));
			Assert.IsNotNull(requestedPath);
			StringAssert.Contains("tone=neutral%2C%20medium%20pitch", requestedPath);
			StringAssert.Contains("characterName=Mimi", requestedPath);
		}

		// ────────────────────────────────────────────────────────────────────
		// FSRS Journal Review Tests
		// ────────────────────────────────────────────────────────────────────

		[Test]
		public void SubmitJournalReview_ShouldPublishError_WhenPayloadIsNull()
		{
			PracticeErrorPayload errorPayload = null;
			EventBus.Subscribe(PracticeEvents.RequestFailed, payload =>
			{
				errorPayload = payload as PracticeErrorPayload;
			});

			PracticeController.HandleSubmitJournalReview(null);

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Missing journal review payload.", errorPayload.Message);
		}

		[Test]
		public void SubmitJournalReview_ShouldPublishError_WhenRatingInvalid()
		{
			PracticeErrorPayload errorPayload = null;
			EventBus.Subscribe(PracticeEvents.RequestFailed, payload =>
			{
				errorPayload = payload as PracticeErrorPayload;
			});

			PracticeController.HandleSubmitJournalReview(new PracticeJournalReviewRequestPayload
			{
				Rating = 5,
				JournalId = 1
			});

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Rating must be between 1 and 4.", errorPayload.Message);
		}

		[Test]
		public void SubmitJournalReview_ShouldPublishError_WhenRatingTooLow()
		{
			PracticeErrorPayload errorPayload = null;
			EventBus.Subscribe(PracticeEvents.RequestFailed, payload =>
			{
				errorPayload = payload as PracticeErrorPayload;
			});

			PracticeController.HandleSubmitJournalReview(new PracticeJournalReviewRequestPayload
			{
				Rating = 0,
				JournalId = 1
			});

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Rating must be between 1 and 4.", errorPayload.Message);
		}

		[Test]
		public void SubmitJournalReview_ShouldPublishError_WhenJournalIdInvalid()
		{
			PracticeErrorPayload errorPayload = null;
			EventBus.Subscribe(PracticeEvents.RequestFailed, payload =>
			{
				errorPayload = payload as PracticeErrorPayload;
			});

			PracticeController.HandleSubmitJournalReview(new PracticeJournalReviewRequestPayload
			{
				Rating = 3,
				JournalId = 0
			});

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Journal id is required for journal review.", errorPayload.Message);
		}

		[UnityTest]
		public IEnumerator LoadFsrsJournals_ShouldPublishLoaded_WhenServerReturnsData()
		{
			PracticeFsrsJournalsLoadedPayload loadedPayload = null;
			EventBus.Subscribe(PracticeEvents.FsrsJournalsLoaded, payload =>
			{
				loadedPayload = payload as PracticeFsrsJournalsLoadedPayload;
			});

			FakeServer.Register("GET", NetworkEndpoints.JournalReviewDue, _ =>
				"{\"journals\":[{\"journalId\":1,\"summary\":\"Test summary\",\"createdAt\":\"2026-01-01T00:00:00Z\",\"review\":null}],\"total\":1}"
			);

			PracticeController.HandleLoadFsrsJournals(null);
			yield return AwaitTask(Task.Delay(100));

			Assert.IsNotNull(loadedPayload);
			Assert.AreEqual(1, loadedPayload.Total);
			Assert.AreEqual(1, loadedPayload.Journals.Count);
			Assert.AreEqual("Test summary", loadedPayload.Journals[0].Summary);
		}

		[UnityTest]
		public IEnumerator SubmitJournalReview_ShouldPublishSubmitted_WhenValid()
		{
			PracticeJournalReviewRequestPayload submittedPayload = null;
			EventBus.Subscribe(PracticeEvents.JournalReviewSubmitted, payload =>
			{
				submittedPayload = payload as PracticeJournalReviewRequestPayload;
			});

			FakeServer.Register("POST", NetworkEndpoints.JournalReview, _ =>
				"{\"journal\":{\"journalId\":1,\"summary\":\"Test\"},\"review\":{\"id\":1,\"journalId\":1}}"
			);

			PracticeController.HandleSubmitJournalReview(new PracticeJournalReviewRequestPayload
			{
				Rating = 3,
				JournalId = 1
			});
			yield return AwaitTask(Task.Delay(100));

			Assert.IsNotNull(submittedPayload);
			Assert.AreEqual(3, submittedPayload.Rating);
			Assert.AreEqual(1, submittedPayload.JournalId);
		}

		private static IEnumerator AwaitTask(Task task)
		{
			while (!task.IsCompleted)
			{
				yield return null;
			}
		}
	}
}
