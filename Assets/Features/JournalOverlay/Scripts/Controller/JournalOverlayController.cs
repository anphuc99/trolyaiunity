using Features.JournalOverlay.Events;
using Features.JournalOverlay.Infrastructure;
using Features.JournalOverlay.Infrastructure.Attributes;
using Features.JournalOverlay.Model;
using Features.JournalOverlay.Requests;
using Core.Infrastructure.Network;
using Core.Infrastructure.State;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Features.JournalOverlay.Controller
{
	/// <summary>
	/// Controller for JournalOverlay feature.
	/// On scope enter, reads selected journal IDs from <see cref="GlobalVariables"/>,
	/// loads their details from the server, builds a playback queue, and publishes
	/// events so the View can auto-play messages with TTS audio.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.JournalOverlayGameplay)]
	public static class JournalOverlayController
	{
		/// <summary>
		/// Called when the controller scope is entered.
		/// Reads selected journal IDs from GlobalVariables and starts playback automatically.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerInit]
		public static void OnEnterScope()
		{
			// Read selected journal IDs from GlobalVariables (set by Journal subfeature).
			if (GlobalVariables.TryGet<List<int>>(JournalOverlayGlobalKeys.SelectedJournalIds, out var ids)
				&& ids != null && ids.Count > 0)
			{
				JournalOverlayState.SelectedJournalIds = new List<int>(ids);
			}
			else
			{
				JournalOverlayState.SelectedJournalIds.Clear();
				PublishError("No journals selected for overlay playback.");
			}
		}

		/// <summary>
		/// Called when the controller scope is exited.
		/// Stops any active playback and clears state.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerShutdown]
		public static void OnExitScope()
		{
			if (JournalOverlayState.IsPlaying)
			{
				StopPlaybackInternal();
			}
			JournalOverlayState.ResetAll();

			// Clean up the global variable.
			GlobalVariables.Remove(JournalOverlayGlobalKeys.SelectedJournalIds);
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(JournalOverlayRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(JournalOverlayEvents.Echoed, payload);
		}

		// ==================================================================
		// Playback handlers
		// ==================================================================

		/// <summary>
		/// Starts loading journal details and building the playback queue.
		/// Called by the View after scope enters and UI is ready.
		/// </summary>
		[Request(JournalOverlayRequests.StartPlayback)]
		public static void HandleStartPlayback(object payload)
		{
			if (JournalOverlayState.SelectedJournalIds.Count == 0)
			{
				PublishError("No journals selected for playback.");
				return;
			}

			_ = StartPlaybackInternalAsync();
		}

		/// <summary>
		/// Pauses current playback.
		/// </summary>
		[Request(JournalOverlayRequests.PausePlayback)]
		public static void HandlePausePlayback(object payload)
		{
			if (!JournalOverlayState.IsPlaying || JournalOverlayState.IsPaused)
			{
				return;
			}

			JournalOverlayState.IsPaused = true;
			EventBus.Publish(JournalOverlayEvents.PlaybackPaused, BuildPlaybackStatePayload());
		}

		/// <summary>
		/// Resumes paused playback.
		/// </summary>
		[Request(JournalOverlayRequests.ResumePlayback)]
		public static void HandleResumePlayback(object payload)
		{
			if (!JournalOverlayState.IsPlaying || !JournalOverlayState.IsPaused)
			{
				return;
			}

			JournalOverlayState.IsPaused = false;
			EventBus.Publish(JournalOverlayEvents.PlaybackResumed, BuildPlaybackStatePayload());
		}

		/// <summary>
		/// Skips to the next message in the playback queue.
		/// </summary>
		[Request(JournalOverlayRequests.NextMessage)]
		public static void HandleNextMessage(object payload)
		{
			if (!JournalOverlayState.IsPlaying)
			{
				return;
			}

			AdvanceToIndex(JournalOverlayState.CurrentPlaybackIndex + 1);
		}

		/// <summary>
		/// Goes back to the previous message in the playback queue.
		/// </summary>
		[Request(JournalOverlayRequests.PreviousMessage)]
		public static void HandlePreviousMessage(object payload)
		{
			if (!JournalOverlayState.IsPlaying)
			{
				return;
			}

			var newIndex = JournalOverlayState.CurrentPlaybackIndex - 1;
			if (newIndex < 0)
			{
				newIndex = 0;
			}

			AdvanceToIndex(newIndex);
		}

		/// <summary>
		/// Stops playback completely and resets state.
		/// </summary>
		[Request(JournalOverlayRequests.StopPlayback)]
		public static void HandleStopPlayback(object payload)
		{
			StopPlaybackInternal();
		}

		/// <summary>
		/// Internal: View calls this when audio finishes to advance to the next message.
		/// </summary>
		[Request(JournalOverlayRequests.AdvancePlayback)]
		public static void HandleAdvancePlayback(object payload)
		{
			if (!JournalOverlayState.IsPlaying)
			{
				return;
			}

			AdvanceToIndex(JournalOverlayState.CurrentPlaybackIndex + 1);
		}

		/// <summary>
		/// Closes the overlay: restores window, stops playback, and signals scene unload.
		/// </summary>
		[Request(JournalOverlayRequests.Close)]
		public static void HandleClose(object payload)
		{
			if (JournalOverlayState.IsPlaying)
			{
				StopPlaybackInternal();
			}

			EventBus.Publish(JournalOverlayEvents.CloseRequested, null);
		}

		// ==================================================================
		// Internal async helpers
		// ==================================================================

		/// <summary>
		/// Loads all selected journal details and builds the flattened playback queue.
		/// Journals are ordered from highest ID to lowest (bottom-to-top in the list).
		/// </summary>
		private static async Task StartPlaybackInternalAsync()
		{
			try
			{
				EventBus.Publish(JournalOverlayEvents.Loading, true);

				var selectedIds = new List<int>(JournalOverlayState.SelectedJournalIds);
				// Sort descending so bottom journal (highest index in list) plays first.
				selectedIds.Sort((a, b) => b.CompareTo(a));

				// Load details for each journal.
				foreach (var journalId in selectedIds)
				{
					if (!JournalOverlayState.CachedDetails.ContainsKey(journalId))
					{
						var detail = await LoadJournalDetailAsync(journalId);
						if (detail != null)
						{
							JournalOverlayState.CachedDetails[journalId] = detail;
						}
					}
				}

				// Build flattened queue from all journal messages.
				var queue = new List<OverlayPlaybackQueueItem>();
				var queueIndex = 0;
				foreach (var journalId in selectedIds)
				{
					if (!JournalOverlayState.CachedDetails.TryGetValue(journalId, out var detail))
					{
						continue;
					}

					var messages = detail.Messages;
					if (messages == null)
					{
						continue;
					}

					for (var i = 0; i < messages.Count; i++)
					{
						var msg = messages[i];
						queue.Add(new OverlayPlaybackQueueItem
						{
							JournalId = journalId,
							MessageId = msg.Id,
							QueueIndex = queueIndex,
							CharacterName = msg.CharacterName,
							Text = msg.Content,
							Tone = msg.Tone,
							SenderName = msg.CharacterName,
							Translation = msg.Translation
						});
						queueIndex++;
					}
				}

				EventBus.Publish(JournalOverlayEvents.Loading, false);

				if (queue.Count == 0)
				{
					PublishError("No messages found in selected journals.");
					return;
				}

				JournalOverlayState.PlaybackQueue = queue;
				JournalOverlayState.CurrentPlaybackIndex = 0;
				JournalOverlayState.IsPlaying = true;
				JournalOverlayState.IsPaused = false;

				EventBus.Publish(JournalOverlayEvents.PlaybackStarted, BuildPlaybackStatePayload());

				// Request TTS for first message.
				await RequestAndPublishPlaybackAudioAsync(0);
			}
			catch (Exception exception)
			{
				EventBus.Publish(JournalOverlayEvents.Loading, false);
				PublishError("Failed to start playback: " + exception.Message);
			}
		}

		/// <summary>
		/// Loads a single journal detail from the server.
		/// </summary>
		/// <param name="journalId">Journal ID to load.</param>
		/// <returns>Detail response, or null on failure.</returns>
		private static async Task<OverlayJournalDetailResponse> LoadJournalDetailAsync(int journalId)
		{
			try
			{
				var endpoint = NetworkEndpoints.Journals + "/" + journalId;
				var responseJson = await HttpClient.GetTaskAsync(endpoint);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					return null;
				}

				var response = JsonConvert.DeserializeObject<OverlayJournalDetailResponse>(responseJson);
				if (response?.Journal == null)
				{
					return null;
				}

				if (response.Messages == null)
				{
					response.Messages = new List<OverlayJournalMessage>();
				}

				return response;
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[JournalOverlayController] Failed to load journal "
					+ journalId + ": " + exception.Message);
				return null;
			}
		}

		/// <summary>
		/// Requests TTS for the message at the given queue index and publishes
		/// <see cref="JournalOverlayEvents.PlaybackMessageChanged"/> with the audio URL.
		/// </summary>
		/// <param name="queueIndex">Index in the playback queue.</param>
		private static async Task RequestAndPublishPlaybackAudioAsync(int queueIndex)
		{
			var queue = JournalOverlayState.PlaybackQueue;
			if (queueIndex < 0 || queueIndex >= queue.Count)
			{
				return;
			}

			var item = queue[queueIndex];
			string audioUrl = null;

			// Only request TTS for non-empty character messages.
			if (!string.IsNullOrWhiteSpace(item.Text))
			{
				try
				{
					var endpoint = BuildTextToSpeechEndpoint(item.Text, item.Tone, item.CharacterName);
					var responseJson = await HttpClient.GetTaskAsync(endpoint);
					if (!string.IsNullOrWhiteSpace(responseJson))
					{
						var ttsResponse = JsonConvert.DeserializeObject<OverlayTtsResponse>(responseJson);
						if (ttsResponse != null && !string.IsNullOrWhiteSpace(ttsResponse.Url))
						{
							audioUrl = ttsResponse.Url;
						}
					}
				}
				catch (Exception exception)
				{
					Debug.LogWarning("[JournalOverlayController] TTS request failed for item "
						+ queueIndex + ": " + exception.Message);
				}
			}

			EventBus.Publish(JournalOverlayEvents.PlaybackMessageChanged, new OverlayPlaybackMessagePayload
			{
				CurrentItem = item,
				CurrentIndex = queueIndex,
				TotalCount = queue.Count,
				AudioUrl = audioUrl
			});
		}

		/// <summary>
		/// Advances playback to a specific queue index, or stops if past the end.
		/// </summary>
		/// <param name="newIndex">Target queue index.</param>
		private static void AdvanceToIndex(int newIndex)
		{
			if (newIndex >= JournalOverlayState.PlaybackQueue.Count)
			{
				StopPlaybackInternal();
				return;
			}

			JournalOverlayState.CurrentPlaybackIndex = newIndex;
			JournalOverlayState.IsPaused = false;
			_ = RequestAndPublishPlaybackAudioAsync(newIndex);
		}

		/// <summary>
		/// Stops playback, resets state, and publishes stopped event.
		/// </summary>
		private static void StopPlaybackInternal()
		{
			JournalOverlayState.ResetPlayback();

			EventBus.Publish(JournalOverlayEvents.PlaybackStopped, new OverlayPlaybackStatePayload
			{
				IsPlaying = false,
				IsPaused = false,
				CurrentIndex = 0,
				TotalCount = 0
			});
		}

		/// <summary>
		/// Builds a <see cref="OverlayPlaybackStatePayload"/> from current state.
		/// </summary>
		/// <returns>Current playback state payload.</returns>
		private static OverlayPlaybackStatePayload BuildPlaybackStatePayload()
		{
			return new OverlayPlaybackStatePayload
			{
				IsPlaying = JournalOverlayState.IsPlaying,
				IsPaused = JournalOverlayState.IsPaused,
				CurrentIndex = JournalOverlayState.CurrentPlaybackIndex,
				TotalCount = JournalOverlayState.PlaybackQueue.Count
			};
		}

		/// <summary>
		/// Builds the text-to-speech endpoint with query parameters.
		/// </summary>
		/// <param name="text">Message text.</param>
		/// <param name="tone">Optional tone hint.</param>
		/// <param name="characterName">Character display name.</param>
		/// <returns>Endpoint URL path with query string.</returns>
		private static string BuildTextToSpeechEndpoint(string text, string tone, string characterName)
		{
			var safeText = string.IsNullOrWhiteSpace(text) ? string.Empty : text;
			var safeTone = string.IsNullOrWhiteSpace(tone) ? "neutral" : tone.Trim();
			var safeName = string.IsNullOrWhiteSpace(characterName) ? string.Empty : characterName.Trim();

			return NetworkEndpoints.TextToSpeech
				+ "?text=" + Uri.EscapeDataString(safeText)
				+ "&tone=" + Uri.EscapeDataString(safeTone)
				+ "&characterName=" + Uri.EscapeDataString(safeName);
		}

		/// <summary>
		/// Publishes a standardized error event.
		/// </summary>
		/// <param name="message">Human-readable error message.</param>
		private static void PublishError(string message)
		{
			EventBus.Publish(JournalOverlayEvents.RequestFailed, new OverlayErrorPayload
			{
				Message = message
			});
		}
	}
}
