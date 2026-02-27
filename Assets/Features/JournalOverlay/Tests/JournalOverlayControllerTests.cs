using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Infrastructure.Network;
using Core.Infrastructure.State;
using Features.JournalOverlay.Controller;
using Features.JournalOverlay.Events;
using Features.JournalOverlay.Model;
using UnityEngine.TestTools;
using NUnit.Framework;

namespace Features.JournalOverlay.Tests
{
	/// <summary>
	/// Tests for JournalOverlay controller.
	/// Covers scope lifecycle, playback controls, and queue navigation.
	/// </summary>
	public sealed class JournalOverlayControllerTests
	{
		[SetUp]
		public void SetUp()
		{
			JournalOverlayState.ResetAll();
			GlobalVariables.Remove(JournalOverlayGlobalKeys.SelectedJournalIds);
			FakeServer.ResetToDefaults();
		}

		[TearDown]
		public void TearDown()
		{
			JournalOverlayState.ResetAll();
			GlobalVariables.Remove(JournalOverlayGlobalKeys.SelectedJournalIds);
			FakeServer.ResetToDefaults();
		}

		// ==================================================================
		// Scope lifecycle
		// ==================================================================

		[Test]
		public void OnEnterScope_WithGlobalVariable_ReadsSelectedIds()
		{
			GlobalVariables.Set(JournalOverlayGlobalKeys.SelectedJournalIds, new List<int> { 1, 2, 3 });

			JournalOverlayController.OnEnterScope();

			Assert.AreEqual(3, JournalOverlayState.SelectedJournalIds.Count);
			Assert.IsTrue(JournalOverlayState.SelectedJournalIds.Contains(1));
			Assert.IsTrue(JournalOverlayState.SelectedJournalIds.Contains(2));
			Assert.IsTrue(JournalOverlayState.SelectedJournalIds.Contains(3));
		}

		[Test]
		public void OnEnterScope_WithoutGlobalVariable_PublishesError()
		{
			OverlayErrorPayload errorPayload = null;
			void Handler(object payload) { errorPayload = payload as OverlayErrorPayload; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalOverlayEvents.RequestFailed, Handler);
			try
			{
				JournalOverlayController.OnEnterScope();
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalOverlayEvents.RequestFailed, Handler);
			}

			Assert.IsNotNull(errorPayload);
			Assert.IsFalse(string.IsNullOrWhiteSpace(errorPayload.Message));
			Assert.AreEqual(0, JournalOverlayState.SelectedJournalIds.Count);
		}

		[Test]
		public void OnExitScope_StopsPlaybackAndClearsState()
		{
			// Arrange: simulate active playback.
			JournalOverlayState.IsPlaying = true;
			JournalOverlayState.PlaybackQueue.Add(new OverlayPlaybackQueueItem { QueueIndex = 0, Text = "test" });
			GlobalVariables.Set(JournalOverlayGlobalKeys.SelectedJournalIds, new List<int> { 1 });

			JournalOverlayController.OnExitScope();

			Assert.IsFalse(JournalOverlayState.IsPlaying);
			Assert.AreEqual(0, JournalOverlayState.PlaybackQueue.Count);
			Assert.AreEqual(0, JournalOverlayState.SelectedJournalIds.Count);
			Assert.IsFalse(GlobalVariables.TryGet<List<int>>(JournalOverlayGlobalKeys.SelectedJournalIds, out _));
		}

		// ==================================================================
		// StartPlayback
		// ==================================================================

		[Test]
		public void HandleStartPlayback_NoSelection_PublishesError()
		{
			OverlayErrorPayload errorPayload = null;
			void Handler(object payload) { errorPayload = payload as OverlayErrorPayload; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalOverlayEvents.RequestFailed, Handler);
			try
			{
				JournalOverlayController.HandleStartPlayback(null);
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalOverlayEvents.RequestFailed, Handler);
			}

			Assert.IsNotNull(errorPayload);
			Assert.IsFalse(string.IsNullOrWhiteSpace(errorPayload.Message));
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleStartPlayback_WithCachedDetail_BuildsQueueAndPublishes()
		{
			JournalOverlayState.SelectedJournalIds = new List<int> { 1 };
			JournalOverlayState.CachedDetails[1] = new OverlayJournalDetailResponse
			{
				Journal = new OverlayJournalSummary { Id = 1, Summary = "Test" },
				Messages = new List<OverlayJournalMessage>
				{
					new OverlayJournalMessage { Id = "m1", Content = "Hello", CharacterName = "Mimi", Tone = "neutral" },
					new OverlayJournalMessage { Id = "m2", Content = "World", CharacterName = "User", Tone = "neutral" }
				}
			};

			FakeServer.Register("GET", NetworkEndpoints.TextToSpeech, _ =>
				"{\"output\":\"audio-id\",\"url\":\"/audio/test.mp3\"}"
			);

			OverlayPlaybackStatePayload startedPayload = null;
			OverlayPlaybackMessagePayload messagePayload = null;
			void StartHandler(object payload) { startedPayload = payload as OverlayPlaybackStatePayload; }
			void MessageHandler(object payload) { messagePayload = payload as OverlayPlaybackMessagePayload; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalOverlayEvents.PlaybackStarted, StartHandler);
			Core.Infrastructure.Events.EventBus.Subscribe(JournalOverlayEvents.PlaybackMessageChanged, MessageHandler);
			try
			{
				JournalOverlayController.HandleStartPlayback(null);
				yield return AwaitTask(Task.Delay(200));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalOverlayEvents.PlaybackStarted, StartHandler);
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalOverlayEvents.PlaybackMessageChanged, MessageHandler);
			}

			Assert.IsNotNull(startedPayload, "PlaybackStarted should be published");
			Assert.IsTrue(startedPayload.IsPlaying);
			Assert.AreEqual(2, startedPayload.TotalCount);

			Assert.IsNotNull(messagePayload, "PlaybackMessageChanged should be published");
			Assert.AreEqual(0, messagePayload.CurrentIndex);
			Assert.AreEqual("Hello", messagePayload.CurrentItem.Text);
			Assert.AreEqual("/audio/test.mp3", messagePayload.AudioUrl);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleStartPlayback_MultipleJournals_OrdersDescending()
		{
			JournalOverlayState.SelectedJournalIds = new List<int> { 1, 2 };
			JournalOverlayState.CachedDetails[1] = new OverlayJournalDetailResponse
			{
				Journal = new OverlayJournalSummary { Id = 1, Summary = "Journal 1" },
				Messages = new List<OverlayJournalMessage>
				{
					new OverlayJournalMessage { Id = "j1m1", Content = "J1Msg", CharacterName = "Mimi", Tone = "neutral" }
				}
			};
			JournalOverlayState.CachedDetails[2] = new OverlayJournalDetailResponse
			{
				Journal = new OverlayJournalSummary { Id = 2, Summary = "Journal 2" },
				Messages = new List<OverlayJournalMessage>
				{
					new OverlayJournalMessage { Id = "j2m1", Content = "J2Msg", CharacterName = "Mimi", Tone = "neutral" }
				}
			};

			FakeServer.Register("GET", NetworkEndpoints.TextToSpeech, _ =>
				"{\"output\":\"audio-id\",\"url\":\"/audio/multi.mp3\"}"
			);

			OverlayPlaybackMessagePayload messagePayload = null;
			void Handler(object payload) { messagePayload = payload as OverlayPlaybackMessagePayload; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalOverlayEvents.PlaybackMessageChanged, Handler);
			try
			{
				JournalOverlayController.HandleStartPlayback(null);
				yield return AwaitTask(Task.Delay(200));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalOverlayEvents.PlaybackMessageChanged, Handler);
			}

			Assert.IsNotNull(messagePayload);
			// Journal 2 comes first (descending sort).
			Assert.AreEqual("J2Msg", messagePayload.CurrentItem.Text);
			Assert.AreEqual(2, JournalOverlayState.PlaybackQueue.Count);
		}

		// ==================================================================
		// Pause / Resume
		// ==================================================================

		[Test]
		public void HandlePausePlayback_WhenPlaying_SetsPausedAndPublishes()
		{
			JournalOverlayState.IsPlaying = true;
			JournalOverlayState.IsPaused = false;

			OverlayPlaybackStatePayload pausedPayload = null;
			void Handler(object payload) { pausedPayload = payload as OverlayPlaybackStatePayload; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalOverlayEvents.PlaybackPaused, Handler);
			try
			{
				JournalOverlayController.HandlePausePlayback(null);
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalOverlayEvents.PlaybackPaused, Handler);
			}

			Assert.IsNotNull(pausedPayload);
			Assert.IsTrue(pausedPayload.IsPaused);
			Assert.IsTrue(JournalOverlayState.IsPaused);
		}

		[Test]
		public void HandlePausePlayback_WhenNotPlaying_DoesNotPublish()
		{
			JournalOverlayState.IsPlaying = false;

			OverlayPlaybackStatePayload pausedPayload = null;
			void Handler(object payload) { pausedPayload = payload as OverlayPlaybackStatePayload; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalOverlayEvents.PlaybackPaused, Handler);
			try
			{
				JournalOverlayController.HandlePausePlayback(null);
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalOverlayEvents.PlaybackPaused, Handler);
			}

			Assert.IsNull(pausedPayload);
		}

		[Test]
		public void HandleResumePlayback_WhenPaused_ClearsPauseAndPublishes()
		{
			JournalOverlayState.IsPlaying = true;
			JournalOverlayState.IsPaused = true;

			OverlayPlaybackStatePayload resumedPayload = null;
			void Handler(object payload) { resumedPayload = payload as OverlayPlaybackStatePayload; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalOverlayEvents.PlaybackResumed, Handler);
			try
			{
				JournalOverlayController.HandleResumePlayback(null);
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalOverlayEvents.PlaybackResumed, Handler);
			}

			Assert.IsNotNull(resumedPayload);
			Assert.IsFalse(resumedPayload.IsPaused);
			Assert.IsFalse(JournalOverlayState.IsPaused);
		}

		// ==================================================================
		// Stop
		// ==================================================================

		[Test]
		public void HandleStopPlayback_ResetsStateAndPublishes()
		{
			JournalOverlayState.IsPlaying = true;
			JournalOverlayState.PlaybackQueue.Add(new OverlayPlaybackQueueItem { QueueIndex = 0, Text = "test" });

			OverlayPlaybackStatePayload stoppedPayload = null;
			void Handler(object payload) { stoppedPayload = payload as OverlayPlaybackStatePayload; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalOverlayEvents.PlaybackStopped, Handler);
			try
			{
				JournalOverlayController.HandleStopPlayback(null);
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalOverlayEvents.PlaybackStopped, Handler);
			}

			Assert.IsNotNull(stoppedPayload);
			Assert.IsFalse(stoppedPayload.IsPlaying);
			Assert.IsFalse(JournalOverlayState.IsPlaying);
			Assert.AreEqual(0, JournalOverlayState.PlaybackQueue.Count);
		}

		// ==================================================================
		// Next / Previous
		// ==================================================================

		[UnityTest]
		public System.Collections.IEnumerator HandleNextMessage_AdvancesIndex()
		{
			SetupActivePlaybackQueue(3);

			FakeServer.Register("GET", NetworkEndpoints.TextToSpeech, _ =>
				"{\"output\":\"audio-id\",\"url\":\"/audio/next.mp3\"}"
			);

			OverlayPlaybackMessagePayload messagePayload = null;
			void Handler(object payload) { messagePayload = payload as OverlayPlaybackMessagePayload; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalOverlayEvents.PlaybackMessageChanged, Handler);
			try
			{
				JournalOverlayController.HandleNextMessage(null);
				yield return AwaitTask(Task.Delay(200));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalOverlayEvents.PlaybackMessageChanged, Handler);
			}

			Assert.IsNotNull(messagePayload);
			Assert.AreEqual(1, messagePayload.CurrentIndex);
			Assert.AreEqual("Message 1", messagePayload.CurrentItem.Text);
		}

		[Test]
		public void HandleNextMessage_AtEnd_StopsPlayback()
		{
			SetupActivePlaybackQueue(1);

			OverlayPlaybackStatePayload stoppedPayload = null;
			void Handler(object payload) { stoppedPayload = payload as OverlayPlaybackStatePayload; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalOverlayEvents.PlaybackStopped, Handler);
			try
			{
				JournalOverlayController.HandleNextMessage(null);
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalOverlayEvents.PlaybackStopped, Handler);
			}

			Assert.IsNotNull(stoppedPayload);
			Assert.IsFalse(stoppedPayload.IsPlaying);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandlePreviousMessage_AtStart_StaysAtZero()
		{
			SetupActivePlaybackQueue(2);

			FakeServer.Register("GET", NetworkEndpoints.TextToSpeech, _ =>
				"{\"output\":\"audio-id\",\"url\":\"/audio/prev.mp3\"}"
			);

			OverlayPlaybackMessagePayload messagePayload = null;
			void Handler(object payload) { messagePayload = payload as OverlayPlaybackMessagePayload; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalOverlayEvents.PlaybackMessageChanged, Handler);
			try
			{
				JournalOverlayController.HandlePreviousMessage(null);
				yield return AwaitTask(Task.Delay(200));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalOverlayEvents.PlaybackMessageChanged, Handler);
			}

			Assert.IsNotNull(messagePayload);
			Assert.AreEqual(0, messagePayload.CurrentIndex);
		}

		// ==================================================================
		// AdvancePlayback
		// ==================================================================

		[UnityTest]
		public System.Collections.IEnumerator HandleAdvancePlayback_AdvancesAndPublishes()
		{
			SetupActivePlaybackQueue(2);

			FakeServer.Register("GET", NetworkEndpoints.TextToSpeech, _ =>
				"{\"output\":\"audio-id\",\"url\":\"/audio/advance.mp3\"}"
			);

			OverlayPlaybackMessagePayload messagePayload = null;
			void Handler(object payload) { messagePayload = payload as OverlayPlaybackMessagePayload; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalOverlayEvents.PlaybackMessageChanged, Handler);
			try
			{
				JournalOverlayController.HandleAdvancePlayback(null);
				yield return AwaitTask(Task.Delay(200));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalOverlayEvents.PlaybackMessageChanged, Handler);
			}

			Assert.IsNotNull(messagePayload);
			Assert.AreEqual(1, messagePayload.CurrentIndex);
			Assert.AreEqual("/audio/advance.mp3", messagePayload.AudioUrl);
		}

		// ==================================================================
		// Close
		// ==================================================================

		[Test]
		public void HandleClose_PublishesCloseRequested()
		{
			bool closeFired = false;
			void Handler(object payload) { closeFired = true; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalOverlayEvents.CloseRequested, Handler);
			try
			{
				JournalOverlayController.HandleClose(null);
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalOverlayEvents.CloseRequested, Handler);
			}

			Assert.IsTrue(closeFired);
		}

		[Test]
		public void HandleClose_WhenPlaying_StopsPlaybackFirst()
		{
			JournalOverlayState.IsPlaying = true;
			JournalOverlayState.PlaybackQueue.Add(new OverlayPlaybackQueueItem { QueueIndex = 0, Text = "test" });

			OverlayPlaybackStatePayload stoppedPayload = null;
			void StopHandler(object payload) { stoppedPayload = payload as OverlayPlaybackStatePayload; }

			bool closeFired = false;
			void CloseHandler(object payload) { closeFired = true; }

			Core.Infrastructure.Events.EventBus.Subscribe(JournalOverlayEvents.PlaybackStopped, StopHandler);
			Core.Infrastructure.Events.EventBus.Subscribe(JournalOverlayEvents.CloseRequested, CloseHandler);
			try
			{
				JournalOverlayController.HandleClose(null);
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalOverlayEvents.PlaybackStopped, StopHandler);
				Core.Infrastructure.Events.EventBus.Unsubscribe(JournalOverlayEvents.CloseRequested, CloseHandler);
			}

			Assert.IsNotNull(stoppedPayload, "Playback should be stopped before closing");
			Assert.IsTrue(closeFired, "CloseRequested should be published");
		}

		// ==================================================================
		// Helpers
		// ==================================================================

		/// <summary>
		/// Sets up JournalOverlayState with an active playback queue of the given size.
		/// </summary>
		/// <param name="count">Number of items in the queue.</param>
		private static void SetupActivePlaybackQueue(int count)
		{
			JournalOverlayState.IsPlaying = true;
			JournalOverlayState.CurrentPlaybackIndex = 0;
			var queue = new List<OverlayPlaybackQueueItem>();
			for (int i = 0; i < count; i++)
			{
				queue.Add(new OverlayPlaybackQueueItem
				{
					QueueIndex = i,
					Text = "Message " + i,
					CharacterName = "Mimi",
					Tone = "neutral"
				});
			}
			JournalOverlayState.PlaybackQueue = queue;
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
