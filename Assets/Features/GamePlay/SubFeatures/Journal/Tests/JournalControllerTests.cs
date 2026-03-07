using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Infrastructure.Attributes;
using Core.Infrastructure.Network;
using Core.Infrastructure.State;
using Features.GamePlay.SubFeatures.Journal.Controller;
using Features.GamePlay.SubFeatures.Journal.Events;
using Features.GamePlay.SubFeatures.Journal.Model;
using UnityEngine.TestTools;
using NUnit.Framework;

namespace Features.GamePlay.SubFeatures.Journal.Tests
{
	/// <summary>
	/// Tests for Journal controller request and event flow.
	/// Playback control tests have been moved to JournalOverlay feature tests.
	/// </summary>
	public sealed class JournalControllerTests
	{
		[ControllerScope(ControllerScopeKey.Global)]
		private static class GlobalVariablesMutationProxyController
		{
			public static void Set(string key, object value)
			{
				GlobalVariables.Set(key, value);
			}

			public static void Remove(string key)
			{
				GlobalVariables.Remove(key);
			}
		}

		[TearDown]
		public void TearDown()
		{
			FakeServer.ResetToDefaults();
			GlobalVariablesMutationProxyController.Remove("global.journal.overlay.selected.ids");
			GlobalVariablesMutationProxyController.Remove(GlobalModes.JournalApiModeKey);
		}

		[Test]
		public void HandleShowJournalList_PublishesViewModeChanged()
		{
			JournalViewModePayload publishedPayload = null;
			void Handler(object payload)
			{
				publishedPayload = payload as JournalViewModePayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.ViewModeChanged, Handler);
			try
			{
				JournalController.HandleShowJournalList();
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.ViewModeChanged, Handler);
			}

			Assert.IsNotNull(publishedPayload);
			Assert.IsFalse(publishedPayload.ShowDetail);
		}

		[Test]
		public void HandleLoadJournalDetail_InvalidPayload_PublishesRequestFailed()
		{
			JournalErrorPayload errorPayload = null;
			void Handler(object payload)
			{
				errorPayload = payload as JournalErrorPayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.RequestFailed, Handler);
			try
			{
				JournalController.HandleLoadJournalDetail("invalid-id");
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.RequestFailed, Handler);
			}

			Assert.IsNotNull(errorPayload);
			Assert.IsFalse(string.IsNullOrWhiteSpace(errorPayload.Message));
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleLoadJournals_ValidResponse_PublishesJournalsLoaded()
		{
			JournalListResponsePayload listPayload = null;
			void Handler(object payload)
			{
				listPayload = payload as JournalListResponsePayload;
			}

			FakeServer.Register("GET", NetworkEndpoints.Journals, _ =>
				"{\"journals\":[{\"id\":1,\"summary\":\"Summary\",\"createdAt\":\"2026-02-25T10:00:00.000Z\"}]}"
			);

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.JournalsLoaded, Handler);
			try
			{
				JournalController.HandleLoadJournals(new JournalListRequestPayload());
				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.JournalsLoaded, Handler);
			}

			Assert.IsNotNull(listPayload);
			Assert.AreEqual(1, listPayload.Journals.Count);
			Assert.AreEqual(1, listPayload.Journals[0].Id);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleLoadJournals_WhenMyLogMode_UsesMyLogJournalsEndpoint()
		{
			GlobalVariablesMutationProxyController.Set(GlobalModes.JournalApiModeKey, GlobalModes.ModeMyLog);

			JournalListResponsePayload listPayload = null;
			void Handler(object payload)
			{
				listPayload = payload as JournalListResponsePayload;
			}

			FakeServer.Register("GET", NetworkEndpoints.MyLogJournals, _ =>
				"{\"journals\":[{\"id\":5,\"summary\":\"MyLog summary\",\"createdAt\":\"2026-02-25T10:00:00.000Z\"}]}"
			);

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.JournalsLoaded, Handler);
			try
			{
				JournalController.HandleLoadJournals(new JournalListRequestPayload());
				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.JournalsLoaded, Handler);
			}

			Assert.IsNotNull(listPayload);
			Assert.AreEqual(1, listPayload.Journals.Count);
			Assert.AreEqual(5, listPayload.Journals[0].Id);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandlePlayMessageAudio_ValidResponse_PublishesAudioEvent()
		{
			JournalPlayMessageAudioPayload audioPayload = null;
			void Handler(object payload)
			{
				audioPayload = payload as JournalPlayMessageAudioPayload;
			}

			FakeServer.Register("GET", NetworkEndpoints.TextToSpeech, _ =>
				"{\"output\":\"audio-id\",\"url\":\"/public/audio.mp3\"}"
			);

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.MessageAudioPlayRequested, Handler);
			try
			{
				JournalController.HandlePlayMessageAudio(new JournalPlayMessageAudioRequestPayload
				{
					MessageId = "m1",
					MessageIndex = 1,
					CharacterName = "Mimi",
					Text = "Hello",
					Tone = "neutral",
					ForceReload = false
				});
				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.MessageAudioPlayRequested, Handler);
			}

			Assert.IsNotNull(audioPayload);
			Assert.AreEqual("m1", audioPayload.MessageId);
			Assert.AreEqual("/public/audio.mp3", audioPayload.AudioUrl);
		}

		// ==================================================================
		// StartPlayback tests (now delegates to JournalOverlay via GlobalVariables)
		// ==================================================================

		[Test]
		public void HandleStartPlayback_NoSelection_PublishesError()
		{
			JournalErrorPayload errorPayload = null;
			void Handler(object payload)
			{
				errorPayload = payload as JournalErrorPayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.RequestFailed, Handler);
			try
			{
				JournalController.HandleStartPlayback(new JournalStartPlaybackRequestPayload());
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.RequestFailed, Handler);
			}

			Assert.IsNotNull(errorPayload);
			Assert.IsFalse(string.IsNullOrWhiteSpace(errorPayload.Message));
		}

		[Test]
		public void HandleStartPlayback_WithSelection_SetsGlobalVariable()
		{
			// HandleStartPlayback sets GlobalVariables and calls LoadScene.ByScope.
			// LoadScene.ByScope may throw in test context; catch that.
			try
			{
				JournalController.HandleStartPlayback(new JournalStartPlaybackRequestPayload
				{
					SelectedIds = new List<int> { 1, 2 }
				});
			}
			catch (System.Exception)
			{
				// Expected — LoadScene.ByScope may fail outside Unity runtime.
			}

			// Verify the global variable was set with the selected ids.
			Assert.IsTrue(
				GlobalVariables.TryGet<List<int>>("global.journal.overlay.selected.ids", out var ids),
				"GlobalVariables should contain the selected journal IDs.");
			Assert.IsNotNull(ids);
			Assert.IsTrue(ids.Contains(1));
			Assert.IsTrue(ids.Contains(2));
		}

		private static System.Collections.IEnumerator AwaitTask(Task task)
		{
			while (!task.IsCompleted)
			{
				yield return null;
			}
		}

		// ==================================================================
		// FSRS Journal Review tests
		// ==================================================================

		[Test]
		public void HandleSubmitJournalReview_NullPayload_PublishesError()
		{
			JournalErrorPayload errorPayload = null;
			void Handler(object payload)
			{
				errorPayload = payload as JournalErrorPayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.RequestFailed, Handler);
			try
			{
				JournalController.HandleSubmitJournalReview(null);
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.RequestFailed, Handler);
			}

			Assert.IsNotNull(errorPayload);
			Assert.IsFalse(string.IsNullOrWhiteSpace(errorPayload.Message));
		}

		[Test]
		public void HandleSubmitJournalReview_InvalidJournalId_PublishesError()
		{
			JournalErrorPayload errorPayload = null;
			void Handler(object payload)
			{
				errorPayload = payload as JournalErrorPayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.RequestFailed, Handler);
			try
			{
				JournalController.HandleSubmitJournalReview(new JournalSubmitReviewRequestPayload
				{
					JournalId = 0,
					Rating = 3
				});
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.RequestFailed, Handler);
			}

			Assert.IsNotNull(errorPayload);
			StringAssert.Contains("journal id", errorPayload.Message.ToLower());
		}

		[Test]
		public void HandleSubmitJournalReview_InvalidRating_PublishesError()
		{
			JournalErrorPayload errorPayload = null;
			void Handler(object payload)
			{
				errorPayload = payload as JournalErrorPayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.RequestFailed, Handler);
			try
			{
				JournalController.HandleSubmitJournalReview(new JournalSubmitReviewRequestPayload
				{
					JournalId = 1,
					Rating = 5
				});
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.RequestFailed, Handler);
			}

			Assert.IsNotNull(errorPayload);
			StringAssert.Contains("rating", errorPayload.Message.ToLower());
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleLoadDueJournals_ValidResponse_PublishesDueJournalsLoaded()
		{
			JournalDueListResponsePayload duePayload = null;
			void Handler(object payload)
			{
				duePayload = payload as JournalDueListResponsePayload;
			}

			FakeServer.Register("GET", NetworkEndpoints.JournalReviewDue, _ =>
				"{\"journals\":[{\"id\":1,\"summary\":\"Due summary\",\"createdAt\":\"2026-03-01T10:00:00.000Z\",\"review\":null}]}"
			);

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.DueJournalsLoaded, Handler);
			try
			{
				JournalController.HandleLoadDueJournals(null);
				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.DueJournalsLoaded, Handler);
			}

			Assert.IsNotNull(duePayload);
			Assert.AreEqual(1, duePayload.Journals.Count);
			Assert.AreEqual(1, duePayload.Journals[0].Id);
			Assert.IsNull(duePayload.Journals[0].Review);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleSubmitJournalReview_ValidResponse_PublishesReviewSubmitted()
		{
			JournalReviewApiResponsePayload reviewPayload = null;
			void Handler(object payload)
			{
				reviewPayload = payload as JournalReviewApiResponsePayload;
			}

			FakeServer.Register("POST", NetworkEndpoints.JournalReview, _ =>
				"{\"journal\":{\"id\":1,\"summary\":\"Test\",\"createdAt\":\"2026-03-01T10:00:00.000Z\"},\"review\":{\"id\":1,\"journalId\":1,\"stability\":3.0,\"difficulty\":5.0,\"lapses\":0,\"currentIntervalDays\":1,\"nextReviewDate\":\"2026-03-02T10:00:00.000Z\",\"lastReviewDate\":\"2026-03-01T10:00:00.000Z\",\"reviewHistory\":[]}}"
			);

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.ReviewSubmitted, Handler);
			try
			{
				JournalController.HandleSubmitJournalReview(new JournalSubmitReviewRequestPayload
				{
					JournalId = 1,
					Rating = 3
				});
				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.ReviewSubmitted, Handler);
			}

			Assert.IsNotNull(reviewPayload);
			Assert.IsNotNull(reviewPayload.Journal);
			Assert.AreEqual(1, reviewPayload.Journal.Id);
			Assert.IsNotNull(reviewPayload.Review);
			Assert.AreEqual(1, reviewPayload.Review.JournalId);
		}
	}
}
