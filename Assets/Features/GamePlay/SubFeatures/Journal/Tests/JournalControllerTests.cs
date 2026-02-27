using System.Collections.Generic;
using System.Threading.Tasks;
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
		[TearDown]
		public void TearDown()
		{
			FakeServer.ResetToDefaults();
			JournalState.SelectedJournalIds.Clear();
			GlobalVariables.Remove("global.journal.overlay.selected.ids");
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
		// Selection tests
		// ==================================================================

		[Test]
		public void HandleToggleJournalSelection_AddsToSet_PublishesEvent()
		{
			JournalSelectionChangedPayload selectionPayload = null;
			void Handler(object payload)
			{
				selectionPayload = payload as JournalSelectionChangedPayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.SelectionChanged, Handler);
			try
			{
				JournalController.HandleToggleJournalSelection(new JournalToggleSelectionPayload { JournalId = 42 });
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.SelectionChanged, Handler);
			}

			Assert.IsNotNull(selectionPayload);
			Assert.IsTrue(selectionPayload.SelectedIds.Contains(42));
		}

		[Test]
		public void HandleToggleJournalSelection_RemovesFromSet_PublishesEvent()
		{
			JournalState.SelectedJournalIds.Add(42);

			JournalSelectionChangedPayload selectionPayload = null;
			void Handler(object payload)
			{
				selectionPayload = payload as JournalSelectionChangedPayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.SelectionChanged, Handler);
			try
			{
				JournalController.HandleToggleJournalSelection(new JournalToggleSelectionPayload { JournalId = 42 });
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.SelectionChanged, Handler);
			}

			Assert.IsNotNull(selectionPayload);
			Assert.IsFalse(selectionPayload.SelectedIds.Contains(42));
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
				JournalController.HandleStartPlayback(null);
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
			JournalState.SelectedJournalIds.Add(1);
			JournalState.SelectedJournalIds.Add(2);

			// HandleStartPlayback sets GlobalVariables and calls LoadScene.ByScope.
			// LoadScene.ByScope may throw in test context; catch that.
			try
			{
				JournalController.HandleStartPlayback(null);
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
	}
}
