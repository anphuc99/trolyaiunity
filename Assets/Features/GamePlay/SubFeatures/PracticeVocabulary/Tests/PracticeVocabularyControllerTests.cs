using NUnit.Framework;
using System;
using System.Threading.Tasks;
using Core.Infrastructure.Events;
using Core.Infrastructure.Network;
using Features.GamePlay.SubFeatures.PracticeVocabulary.Controller;
using Features.GamePlay.SubFeatures.PracticeVocabulary.Events;
using Features.GamePlay.SubFeatures.PracticeVocabulary.Model;
using UnityEngine.TestTools;

namespace Features.GamePlay.SubFeatures.PracticeVocabulary.Tests
{
	/// <summary>
	/// Basic tests for PracticeVocabulary controller.
	/// </summary>
	public sealed class PracticeVocabularyControllerTests
	{
		[SetUp]
		public void SetUp()
		{
			EventBus.ClearAll();
			FakeServer.ResetToDefaults();
			PracticeVocabularyState.ParentSignals = null;
		}

		[TearDown]
		public void TearDown()
		{
			EventBus.ClearAll();
			FakeServer.ResetToDefaults();
			PracticeVocabularyState.ParentSignals = null;
		}

		[Test]
		public void Install_ShouldPublishInstalledEvent()
		{
			var published = false;
			EventBus.Subscribe(PracticeVocabularyEvents.Installed, _ => published = true);

			PracticeVocabularyController.Install();

			Assert.IsTrue(published);
		}

		[Test]
		public void Uninstall_ShouldPublishUninstalledEvent()
		{
			var published = false;
			EventBus.Subscribe(PracticeVocabularyEvents.Uninstalled, _ => published = true);

			PracticeVocabularyController.Uninstall();

			Assert.IsTrue(published);
		}

		[Test]
		public void HandleSubmitReview_ShouldPublishError_WhenPayloadIsNull()
		{
			PracticeVocabularyErrorPayload errorPayload = null;
			EventBus.Subscribe(PracticeVocabularyEvents.RequestFailed, payload =>
			{
				errorPayload = payload as PracticeVocabularyErrorPayload;
			});

			PracticeVocabularyController.HandleSubmitReview(null);

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Missing vocabulary review payload.", errorPayload.Message);
		}

		[Test]
		public void HandleSubmitReview_ShouldPublishError_WhenVocabularyIdMissing()
		{
			PracticeVocabularyErrorPayload errorPayload = null;
			EventBus.Subscribe(PracticeVocabularyEvents.RequestFailed, payload =>
			{
				errorPayload = payload as PracticeVocabularyErrorPayload;
			});

			PracticeVocabularyController.HandleSubmitReview(new PracticeVocabularyReviewRequestPayload
			{
				VocabularyId = " ",
				Rating = 3
			});

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Vocabulary id is required for review.", errorPayload.Message);
		}

		[Test]
		public void HandleSubmitReview_ShouldPublishError_WhenRatingOutOfRange()
		{
			PracticeVocabularyErrorPayload errorPayload = null;
			EventBus.Subscribe(PracticeVocabularyEvents.RequestFailed, payload =>
			{
				errorPayload = payload as PracticeVocabularyErrorPayload;
			});

			PracticeVocabularyController.HandleSubmitReview(new PracticeVocabularyReviewRequestPayload
			{
				VocabularyId = "v1",
				Rating = 5
			});

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Rating must be between 1 and 4.", errorPayload.Message);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleLoadDueReviews_ShouldPublishDueList_WhenServerReturnsData()
		{
			PracticeVocabularyDueListResponsePayload responsePayload = null;
			void Handler(object payload)
			{
				responsePayload = payload as PracticeVocabularyDueListResponsePayload;
			}

			FakeServer.Register("GET", NetworkEndpoints.VocabularyDue,
				_ => "{\"vocabularies\":[{\"id\":\"v1\",\"korean\":\"爱\",\"vietnamese\":\"yêu\",\"pinyin\":\"ài\",\"review\":{\"id\":\"r1\",\"vocabularyId\":\"v1\",\"nextReviewDate\":\"2026-04-09T00:00:00.000Z\"}}],\"total\":1}"
			);

			EventBus.Subscribe(PracticeVocabularyEvents.DueReviewsLoaded, Handler);
			try
			{
				PracticeVocabularyController.HandleLoadDueReviews(null);
				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				EventBus.Unsubscribe(PracticeVocabularyEvents.DueReviewsLoaded, Handler);
			}

			Assert.IsNotNull(responsePayload);
			Assert.AreEqual(1, responsePayload.Total);
			Assert.AreEqual(1, responsePayload.Vocabularies.Count);
			Assert.AreEqual("v1", responsePayload.Vocabularies[0].Id);
			Assert.AreEqual("爱", responsePayload.Vocabularies[0].Korean);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleSubmitReview_ShouldPublishReviewSubmitted_WhenServerReturnsData()
		{
			PracticeVocabularyReviewSubmittedPayload responsePayload = null;
			void Handler(object payload)
			{
				responsePayload = payload as PracticeVocabularyReviewSubmittedPayload;
			}

			FakeServer.Register("POST", NetworkEndpoints.VocabularyReview + "/v1/review",
				_ => "{\"id\":\"r1\",\"vocabularyId\":\"v1\",\"nextReviewDate\":\"2026-04-10T00:00:00.000Z\"}"
			);

			EventBus.Subscribe(PracticeVocabularyEvents.ReviewSubmitted, Handler);
			try
			{
				PracticeVocabularyController.HandleSubmitReview(new PracticeVocabularyReviewRequestPayload
				{
					VocabularyId = "v1",
					Rating = 3
				});
				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				EventBus.Unsubscribe(PracticeVocabularyEvents.ReviewSubmitted, Handler);
			}

			Assert.IsNotNull(responsePayload);
			Assert.AreEqual("v1", responsePayload.VocabularyId);
			Assert.AreEqual(3, responsePayload.Rating);
			Assert.IsNotNull(responsePayload.Review);
			Assert.AreEqual("r1", responsePayload.Review.Id);
		}

		private static System.Collections.IEnumerator AwaitTask(Task task)
		{
			while (!task.IsCompleted)
			{
				yield return null;
			}
		}
	}
}
