using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Infrastructure.Network;
using Features.GamePlay.SubFeatures.Journal.Controller;
using Features.GamePlay.SubFeatures.Journal.Events;
using Features.GamePlay.SubFeatures.Journal.Model;
using UnityEngine.TestTools;
using NUnit.Framework;

namespace Features.GamePlay.SubFeatures.Journal.Tests
{
	/// <summary>
	/// Tests for Journal controller request and event flow.
	/// </summary>
	public sealed class JournalControllerTests
	{
		[TearDown]
		public void TearDown()
		{
			FakeServer.ResetToDefaults();
			JournalState.SelectedJournalIds.Clear();
			JournalState.ResetPlaybackState();
			JournalState.CachedDetails.Clear();
			JournalState.IsFloatingMode = false;
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

		[UnityTest]
		public System.Collections.IEnumerator HandleStartPlayback_WithSelection_BuildsQueueAndPublishes()
		{
			// Pre-cache a journal detail so StartPlayback doesn't need HTTP.
			JournalState.SelectedJournalIds.Add(1);
			JournalState.CachedDetails[1] = new JournalDetailResponsePayload
			{
				Journal = new JournalSummaryPayload { Id = 1, Summary = "Test" },
				Messages = new List<JournalMessagePayload>
				{
					new JournalMessagePayload { Id = "m1", Content = "Hello", CharacterName = "Mimi", Tone = "neutral" },
					new JournalMessagePayload { Id = "m2", Content = "World", CharacterName = "User", Tone = "neutral" }
				}
			};

			// Register TTS endpoint for the first message.
			FakeServer.Register("GET", NetworkEndpoints.TextToSpeech, _ =>
				"{\"output\":\"audio-id\",\"url\":\"/audio/test.mp3\"}"
			);

			JournalPlaybackStatePayload startedPayload = null;
			JournalPlaybackMessageChangedPayload messagePayload = null;
			void StartHandler(object payload) { startedPayload = payload as JournalPlaybackStatePayload; }
			void MessageHandler(object payload) { messagePayload = payload as JournalPlaybackMessageChangedPayload; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.PlaybackStarted, StartHandler);
			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.PlaybackMessageChanged, MessageHandler);
			try
			{
				JournalController.HandleStartPlayback(null);
				yield return AwaitTask(Task.Delay(200));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.PlaybackStarted, StartHandler);
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.PlaybackMessageChanged, MessageHandler);
			}

			Assert.IsNotNull(startedPayload, "PlaybackStarted event should be published");
			Assert.IsTrue(startedPayload.IsPlaying);
			Assert.AreEqual(2, startedPayload.TotalCount);

			Assert.IsNotNull(messagePayload, "PlaybackMessageChanged event should be published");
			Assert.AreEqual(0, messagePayload.CurrentIndex);
			Assert.AreEqual("Hello", messagePayload.CurrentItem.Text);
			Assert.AreEqual("/audio/test.mp3", messagePayload.AudioUrl);
		}

		// ==================================================================
		// Playback control tests
		// ==================================================================

		[Test]
		public void HandlePausePlayback_WhenPlaying_PublishesPaused()
		{
			// Arrange: simulate active playback.
			JournalState.IsPlaying = true;
			JournalState.IsPaused = false;

			JournalPlaybackStatePayload pausedPayload = null;
			void Handler(object payload)
			{
				pausedPayload = payload as JournalPlaybackStatePayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.PlaybackPaused, Handler);
			try
			{
				JournalController.HandlePausePlayback(null);
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.PlaybackPaused, Handler);
			}

			Assert.IsNotNull(pausedPayload);
			Assert.IsTrue(pausedPayload.IsPaused);
			Assert.IsTrue(JournalState.IsPaused);
		}

		[Test]
		public void HandleResumePlayback_WhenPaused_PublishesResumed()
		{
			JournalState.IsPlaying = true;
			JournalState.IsPaused = true;

			JournalPlaybackStatePayload resumedPayload = null;
			void Handler(object payload)
			{
				resumedPayload = payload as JournalPlaybackStatePayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.PlaybackResumed, Handler);
			try
			{
				JournalController.HandleResumePlayback(null);
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.PlaybackResumed, Handler);
			}

			Assert.IsNotNull(resumedPayload);
			Assert.IsFalse(resumedPayload.IsPaused);
			Assert.IsFalse(JournalState.IsPaused);
		}

		[Test]
		public void HandleStopPlayback_ResetsState_PublishesStopped()
		{
			JournalState.IsPlaying = true;
			JournalState.PlaybackQueue.Add(new JournalPlaybackQueueItem { QueueIndex = 0, Text = "test" });

			JournalPlaybackStatePayload stoppedPayload = null;
			void Handler(object payload)
			{
				stoppedPayload = payload as JournalPlaybackStatePayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.PlaybackStopped, Handler);
			try
			{
				JournalController.HandleStopPlayback(null);
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.PlaybackStopped, Handler);
			}

			Assert.IsNotNull(stoppedPayload);
			Assert.IsFalse(stoppedPayload.IsPlaying);
			Assert.IsFalse(JournalState.IsPlaying);
			Assert.AreEqual(0, JournalState.PlaybackQueue.Count);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleNextMessage_AdvancesIndex_PublishesChanged()
		{
			// Setup: active playback with 3 items.
			JournalState.IsPlaying = true;
			JournalState.CurrentPlaybackIndex = 0;
			JournalState.PlaybackQueue = new List<JournalPlaybackQueueItem>
			{
				new JournalPlaybackQueueItem { QueueIndex = 0, Text = "First", CharacterName = "Mimi", Tone = "neutral" },
				new JournalPlaybackQueueItem { QueueIndex = 1, Text = "Second", CharacterName = "Mimi", Tone = "neutral" },
				new JournalPlaybackQueueItem { QueueIndex = 2, Text = "Third", CharacterName = "Mimi", Tone = "neutral" }
			};

			FakeServer.Register("GET", NetworkEndpoints.TextToSpeech, _ =>
				"{\"output\":\"audio-id\",\"url\":\"/audio/next.mp3\"}"
			);

			JournalPlaybackMessageChangedPayload messagePayload = null;
			void Handler(object payload) { messagePayload = payload as JournalPlaybackMessageChangedPayload; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.PlaybackMessageChanged, Handler);
			try
			{
				JournalController.HandleNextMessage(null);
				yield return AwaitTask(Task.Delay(200));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.PlaybackMessageChanged, Handler);
			}

			Assert.IsNotNull(messagePayload);
			Assert.AreEqual(1, messagePayload.CurrentIndex);
			Assert.AreEqual("Second", messagePayload.CurrentItem.Text);
		}

		[Test]
		public void HandleNextMessage_AtEnd_PublishesStopped()
		{
			JournalState.IsPlaying = true;
			JournalState.CurrentPlaybackIndex = 0;
			JournalState.PlaybackQueue = new List<JournalPlaybackQueueItem>
			{
				new JournalPlaybackQueueItem { QueueIndex = 0, Text = "Only" }
			};

			JournalPlaybackStatePayload stoppedPayload = null;
			void Handler(object payload) { stoppedPayload = payload as JournalPlaybackStatePayload; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.PlaybackStopped, Handler);
			try
			{
				JournalController.HandleNextMessage(null);
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.PlaybackStopped, Handler);
			}

			Assert.IsNotNull(stoppedPayload);
			Assert.IsFalse(stoppedPayload.IsPlaying);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandlePreviousMessage_AtStart_StaysAtZero()
		{
			JournalState.IsPlaying = true;
			JournalState.CurrentPlaybackIndex = 0;
			JournalState.PlaybackQueue = new List<JournalPlaybackQueueItem>
			{
				new JournalPlaybackQueueItem { QueueIndex = 0, Text = "First", CharacterName = "Mimi", Tone = "neutral" },
				new JournalPlaybackQueueItem { QueueIndex = 1, Text = "Second", CharacterName = "Mimi", Tone = "neutral" }
			};

			FakeServer.Register("GET", NetworkEndpoints.TextToSpeech, _ =>
				"{\"output\":\"audio-id\",\"url\":\"/audio/prev.mp3\"}"
			);

			JournalPlaybackMessageChangedPayload messagePayload = null;
			void Handler(object payload) { messagePayload = payload as JournalPlaybackMessageChangedPayload; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.PlaybackMessageChanged, Handler);
			try
			{
				JournalController.HandlePreviousMessage(null);
				yield return AwaitTask(Task.Delay(200));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.PlaybackMessageChanged, Handler);
			}

			Assert.IsNotNull(messagePayload);
			Assert.AreEqual(0, messagePayload.CurrentIndex);
		}

		[Test]
		public void HandleToggleFloatingMode_TogglesState_PublishesChanged()
		{
			Assert.IsFalse(JournalState.IsFloatingMode);

			JournalFloatingModePayload floatingPayload = null;
			void Handler(object payload) { floatingPayload = payload as JournalFloatingModePayload; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.FloatingModeChanged, Handler);
			try
			{
				JournalController.HandleToggleFloatingMode(null);
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.FloatingModeChanged, Handler);
			}

			Assert.IsNotNull(floatingPayload);
			Assert.IsTrue(floatingPayload.IsFloating);
			Assert.IsTrue(JournalState.IsFloatingMode);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleAdvancePlayback_AdvancesAndRequestsTts()
		{
			JournalState.IsPlaying = true;
			JournalState.CurrentPlaybackIndex = 0;
			JournalState.PlaybackQueue = new List<JournalPlaybackQueueItem>
			{
				new JournalPlaybackQueueItem { QueueIndex = 0, Text = "First", CharacterName = "Mimi", Tone = "neutral" },
				new JournalPlaybackQueueItem { QueueIndex = 1, Text = "Second", CharacterName = "Mimi", Tone = "neutral" }
			};

			FakeServer.Register("GET", NetworkEndpoints.TextToSpeech, _ =>
				"{\"output\":\"audio-id\",\"url\":\"/audio/advance.mp3\"}"
			);

			JournalPlaybackMessageChangedPayload messagePayload = null;
			void Handler(object payload) { messagePayload = payload as JournalPlaybackMessageChangedPayload; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.PlaybackMessageChanged, Handler);
			try
			{
				JournalController.HandleAdvancePlayback(null);
				yield return AwaitTask(Task.Delay(200));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.PlaybackMessageChanged, Handler);
			}

			Assert.IsNotNull(messagePayload);
			Assert.AreEqual(1, messagePayload.CurrentIndex);
			Assert.AreEqual("Second", messagePayload.CurrentItem.Text);
			Assert.AreEqual("/audio/advance.mp3", messagePayload.AudioUrl);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleStartPlayback_MultipleJournals_OrdersBottomToTop()
		{
			// Pre-cache two journals.
			JournalState.SelectedJournalIds.Add(1);
			JournalState.SelectedJournalIds.Add(2);
			JournalState.CachedDetails[1] = new JournalDetailResponsePayload
			{
				Journal = new JournalSummaryPayload { Id = 1, Summary = "First Journal" },
				Messages = new List<JournalMessagePayload>
				{
					new JournalMessagePayload { Id = "j1m1", Content = "Journal1-Msg1", CharacterName = "Mimi", Tone = "neutral" }
				}
			};
			JournalState.CachedDetails[2] = new JournalDetailResponsePayload
			{
				Journal = new JournalSummaryPayload { Id = 2, Summary = "Second Journal" },
				Messages = new List<JournalMessagePayload>
				{
					new JournalMessagePayload { Id = "j2m1", Content = "Journal2-Msg1", CharacterName = "Mimi", Tone = "neutral" }
				}
			};

			FakeServer.Register("GET", NetworkEndpoints.TextToSpeech, _ =>
				"{\"output\":\"audio-id\",\"url\":\"/audio/multi.mp3\"}"
			);

			JournalPlaybackMessageChangedPayload messagePayload = null;
			void Handler(object payload) { messagePayload = payload as JournalPlaybackMessageChangedPayload; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalEvents.PlaybackMessageChanged, Handler);
			try
			{
				JournalController.HandleStartPlayback(null);
				yield return AwaitTask(Task.Delay(200));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalEvents.PlaybackMessageChanged, Handler);
			}

			Assert.IsNotNull(messagePayload);
			// Journal 2 (higher id) should play first due to descending sort.
			Assert.AreEqual("Journal2-Msg1", messagePayload.CurrentItem.Text);
			Assert.AreEqual(2, JournalState.PlaybackQueue.Count);
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
