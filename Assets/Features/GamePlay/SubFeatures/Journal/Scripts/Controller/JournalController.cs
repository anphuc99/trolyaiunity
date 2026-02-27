using Features.GamePlay.SubFeatures.Journal.Events;
using Features.GamePlay.SubFeatures.Journal.Infrastructure;
using Features.GamePlay.SubFeatures.Journal.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Journal.Model;
using Features.GamePlay.SubFeatures.Journal.Requests;
using Core.Infrastructure.Network;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using UnityEngine;
using System.Collections.Generic;
using Share.Components;

namespace Features.GamePlay.SubFeatures.Journal.Controller
{
	/// <summary>
	/// Controller for Journal.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class JournalController
	{
		/// <summary>
		/// Called when the controller scope is entered.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerInit]
		public static void OnEnterScope()
		{
		}

		/// <summary>
		/// Called when the controller scope is exited.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerShutdown]
		public static void OnExitScope()
		{
			// Stop playback if active before clearing state.
			if (JournalState.IsPlaying)
			{
				StopPlaybackInternal();
			}

			JournalState.CachedList = new JournalListResponsePayload();
			JournalState.CachedDetail = null;
			JournalState.SelectedJournalId = null;
			JournalState.ResetAll();
		}

		/// <summary>
		/// Installs the subcontroller.
		/// </summary>
		public static void Install()
		{
			EventBus.Publish(JournalEvents.Installed, null);
		}

		/// <summary>
		/// Uninstalls the subcontroller and clears bound parent signals.
		/// </summary>
		public static void Uninstall()
		{
			if (JournalState.IsPlaying)
			{
				StopPlaybackInternal();
			}
			EventBus.Publish(JournalEvents.Uninstalled, null);
		}

		/// <summary>
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(JournalParentSignals signals)
		{
			JournalState.ParentSignals = signals;
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(JournalRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(JournalEvents.Echoed, payload);
			JournalState.ParentSignals?.OnEchoed?.Invoke(payload);
		}

		/// <summary>
		/// Loads journal list from server.
		/// </summary>
		/// <param name="payload">Optional list request payload.</param>
		[Request(JournalRequests.LoadJournals)]
		public static void HandleLoadJournals(JournalListRequestPayload payload)
		{
			_ = LoadJournalsInternalAsync(payload ?? new JournalListRequestPayload());
		}

		/// <summary>
		/// Loads one journal detail by id.
		/// </summary>
		/// <param name="payload">Detail request payload or raw id.</param>
		[Request(JournalRequests.LoadJournalDetail)]
		public static void HandleLoadJournalDetail(object payload)
		{
			if (!TryResolveJournalId(payload, out var journalId))
			{
				PublishError("Missing or invalid journal id.");
				return;
			}

			_ = LoadJournalDetailInternalAsync(journalId);
		}

		/// <summary>
		/// Switches journal view back to list mode.
		/// </summary>
		[Request(JournalRequests.ShowJournalList)]
		public static void HandleShowJournalList()
		{
			EventBus.Publish(JournalEvents.ViewModeChanged, new JournalViewModePayload
			{
				ShowDetail = false,
				JournalId = null
			});
		}

		/// <summary>
		/// Requests server-generated audio for a journal message.
		/// </summary>
		/// <param name="payload">Audio request payload.</param>
		[Request(JournalRequests.PlayMessageAudio)]
		public static void HandlePlayMessageAudio(JournalPlayMessageAudioRequestPayload payload)
		{
			if (payload == null || string.IsNullOrWhiteSpace(payload.Text))
			{
				PublishError("Missing message content for audio playback.");
				return;
			}

			_ = RequestMessageAudioInternalAsync(payload);
		}

		// ==================================================================
		// Selection handlers
		// ==================================================================

		/// <summary>
		/// Toggles a journal id in/out of the multi-select set.
		/// </summary>
		/// <param name="payload">Toggle selection payload.</param>
		[Request(JournalRequests.ToggleJournalSelection)]
		public static void HandleToggleJournalSelection(JournalToggleSelectionPayload payload)
		{
			if (payload == null || payload.JournalId <= 0)
			{
				PublishError("Invalid journal id for selection.");
				return;
			}

			var ids = JournalState.SelectedJournalIds;
			if (!ids.Remove(payload.JournalId))
			{
				ids.Add(payload.JournalId);
			}

			EventBus.Publish(JournalEvents.SelectionChanged, new JournalSelectionChangedPayload
			{
				SelectedIds = ids
			});
		}

		// ==================================================================
		// Playback handlers
		// ==================================================================

		/// <summary>
		/// Starts sequential playback of all selected journals' messages.
		/// Loads details for journals not yet cached, then flattens messages into a queue.
		/// </summary>
		[Request(JournalRequests.StartPlayback)]
		public static void HandleStartPlayback(object payload)
		{
			if (JournalState.SelectedJournalIds.Count == 0)
			{
				PublishError("No journals selected for playback.");
				return;
			}

			_ = StartPlaybackInternalAsync();
		}

		/// <summary>
		/// Pauses current playback.
		/// </summary>
		[Request(JournalRequests.PausePlayback)]
		public static void HandlePausePlayback(object payload)
		{
			if (!JournalState.IsPlaying || JournalState.IsPaused)
			{
				return;
			}

			JournalState.IsPaused = true;
			EventBus.Publish(JournalEvents.PlaybackPaused, BuildPlaybackStatePayload());
		}

		/// <summary>
		/// Resumes paused playback.
		/// </summary>
		[Request(JournalRequests.ResumePlayback)]
		public static void HandleResumePlayback(object payload)
		{
			if (!JournalState.IsPlaying || !JournalState.IsPaused)
			{
				return;
			}

			JournalState.IsPaused = false;
			EventBus.Publish(JournalEvents.PlaybackResumed, BuildPlaybackStatePayload());
		}

		/// <summary>
		/// Skips to the next message in the playback queue.
		/// </summary>
		[Request(JournalRequests.NextMessage)]
		public static void HandleNextMessage(object payload)
		{
			if (!JournalState.IsPlaying)
			{
				return;
			}

			AdvanceToIndex(JournalState.CurrentPlaybackIndex + 1);
		}

		/// <summary>
		/// Goes back to the previous message in the playback queue.
		/// </summary>
		[Request(JournalRequests.PreviousMessage)]
		public static void HandlePreviousMessage(object payload)
		{
			if (!JournalState.IsPlaying)
			{
				return;
			}

			var newIndex = JournalState.CurrentPlaybackIndex - 1;
			if (newIndex < 0)
			{
				newIndex = 0;
			}

			AdvanceToIndex(newIndex);
		}

		/// <summary>
		/// Stops playback completely and resets state.
		/// </summary>
		[Request(JournalRequests.StopPlayback)]
		public static void HandleStopPlayback(object payload)
		{
			StopPlaybackInternal();
		}

		/// <summary>
		/// Toggles the floating overlay mode.
		/// </summary>
		[Request(JournalRequests.ToggleFloatingMode)]
		public static void HandleToggleFloatingMode(object payload)
		{
			JournalState.IsFloatingMode = !JournalState.IsFloatingMode;
			EventBus.Publish(JournalEvents.FloatingModeChanged, new JournalFloatingModePayload
			{
				IsFloating = JournalState.IsFloatingMode
			});
		}

		/// <summary>
		/// Internal request: View calls this when audio finishes to advance to the next message.
		/// </summary>
		[Request(JournalRequests.AdvancePlayback)]
		public static void HandleAdvancePlayback(object payload)
		{
			if (!JournalState.IsPlaying)
			{
				return;
			}

			AdvanceToIndex(JournalState.CurrentPlaybackIndex + 1);
		}

		/// <summary>
		/// Performs list API call and publishes response.
		/// </summary>
		/// <param name="payload">List request payload.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task LoadJournalsInternalAsync(JournalListRequestPayload payload)
		{
			try
			{
				var endpoint = BuildJournalsEndpoint(payload?.StoryId);
				var responseJson = await HttpClient.GetTaskAsync(endpoint);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Empty journals response from server.");
					return;
				}

				var response = JsonConvert.DeserializeObject<JournalListResponsePayload>(responseJson) ?? new JournalListResponsePayload();
				if (response.Journals == null)
				{
					response.Journals = new System.Collections.Generic.List<JournalListItemPayload>();
				}

				JournalState.CachedList = response;
				EventBus.Publish(JournalEvents.JournalsLoaded, response);
				EventBus.Publish(JournalEvents.ViewModeChanged, new JournalViewModePayload
				{
					ShowDetail = false,
					JournalId = null
				});
			}
			catch (Exception exception)
			{
				PublishError("Failed to load journals: " + exception.Message);
			}
		}

		/// <summary>
		/// Performs detail API call and publishes response.
		/// </summary>
		/// <param name="journalId">Journal id to load.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task LoadJournalDetailInternalAsync(int journalId)
		{
			try
			{
				var endpoint = BuildJournalDetailEndpoint(journalId);
				var responseJson = await HttpClient.GetTaskAsync(endpoint);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Empty journal detail response from server.");
					return;
				}

				var response = JsonConvert.DeserializeObject<JournalDetailResponsePayload>(responseJson);
				if (response == null || response.Journal == null)
				{
					PublishError("Server returned invalid journal detail payload.");
					return;
				}

				if (response.Messages == null)
				{
					response.Messages = new System.Collections.Generic.List<JournalMessagePayload>();
				}

				JournalState.CachedDetail = response;
				JournalState.SelectedJournalId = response.Journal.Id;

				var messages = response.Messages;
				List<MessageBubbleData> messageBubbleDataList = new List<MessageBubbleData>();
				for (int i = 0; i < messages.Count; i++)
				{
					var message = messages[i];
					messageBubbleDataList.Add(new MessageBubbleData
					{
						MessageId = message.Id,
						Type = message.CharacterName == "User" ? MessageBubbleType.User : MessageBubbleType.Character,
						SenderName = message.CharacterName,
						Message = message.Content,
						Avatar = GetAvatar(message.CharacterName), // Avatar can be set based on sender or other logic
						Tone = message.Tone,
						Translation = message.Translation,
						IsTranslationExpanded = false,
						IsTtsReloading = false,
						MessageIndex = i,
					});
				}

				// Activate detail view BEFORE sending data so the container
				// is active when SetMessages measures bubble heights.
				EventBus.Publish(JournalEvents.ViewModeChanged, new JournalViewModePayload
				{
					ShowDetail = true,
					JournalId = response.Journal.Id
				});
				EventBus.Publish(JournalEvents.JournalDetailLoaded, messageBubbleDataList);
			}
			catch (Exception exception)
			{
				PublishError("Failed to load journal detail: " + exception.Message);
			}
		}

		/// <summary>
		/// Performs text-to-speech API call and publishes audio playback payload.
		/// </summary>
		/// <param name="payload">Audio request payload.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task RequestMessageAudioInternalAsync(JournalPlayMessageAudioRequestPayload payload)
		{
			try
			{
				var endpoint = BuildTextToSpeechEndpoint(payload.Text, payload.Tone, payload.CharacterName, payload.ForceReload);
				var responseJson = await HttpClient.GetTaskAsync(endpoint);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Empty text-to-speech response from server.");
					return;
				}

				var response = JsonConvert.DeserializeObject<JournalTextToSpeechResponsePayload>(responseJson);
				if (response == null || string.IsNullOrWhiteSpace(response.Url))
				{
					PublishError("Server returned invalid text-to-speech payload.");
					return;
				}

				EventBus.Publish(JournalEvents.MessageAudioPlayRequested, new JournalPlayMessageAudioPayload
				{
					MessageId = payload.MessageId,
					MessageIndex = payload.MessageIndex,
					CharacterName = payload.CharacterName,
					Text = payload.Text,
					Tone = payload.Tone,
					AudioUrl = response.Url
				});
			}
			catch (Exception exception)
			{
				PublishError("Failed to load journal audio: " + exception.Message);
			}
		}

		/// <summary>
		/// Builds journal list endpoint with optional story id query.
		/// </summary>
		/// <param name="storyId">Optional story id filter.</param>
		/// <returns>Resolved endpoint path.</returns>
		private static string BuildJournalsEndpoint(int? storyId)
		{
			if (!storyId.HasValue || storyId.Value <= 0)
			{
				return NetworkEndpoints.Journals;
			}

			return NetworkEndpoints.Journals + "?storyId=" + Uri.EscapeDataString(storyId.Value.ToString());
		}

		/// <summary>
		/// Builds journal detail endpoint.
		/// </summary>
		/// <param name="journalId">Journal id.</param>
		/// <returns>Resolved endpoint path.</returns>
		private static string BuildJournalDetailEndpoint(int journalId)
		{
			return NetworkEndpoints.Journals + "/" + journalId;
		}

		/// <summary>
		/// Builds text-to-speech endpoint with query parameters.
		/// </summary>
		/// <param name="text">Message text.</param>
		/// <param name="tone">Optional tone hint.</param>
		/// <param name="characterName">Character display name.</param>
		/// <param name="forceReload">True to force regeneration on server.</param>
		/// <returns>Resolved endpoint path.</returns>
		private static string BuildTextToSpeechEndpoint(string text, string tone, string characterName, bool forceReload)
		{
			var safeText = string.IsNullOrWhiteSpace(text) ? string.Empty : text;
			var safeTone = string.IsNullOrWhiteSpace(tone) ? "neutral" : tone.Trim();
			var safeName = string.IsNullOrWhiteSpace(characterName) ? string.Empty : characterName.Trim();
			var endpoint = NetworkEndpoints.TextToSpeech;
			var query = "text=" + Uri.EscapeDataString(safeText)
				+ "&tone=" + Uri.EscapeDataString(safeTone)
				+ "&characterName=" + Uri.EscapeDataString(safeName);

			if (forceReload)
			{
				query += "&force=true";
			}

			return endpoint + "?" + query;
		}

		/// <summary>
		/// Tries to resolve journal id from multiple payload shapes.
		/// </summary>
		/// <param name="payload">Request payload.</param>
		/// <param name="journalId">Resolved journal id.</param>
		/// <returns>True when resolution succeeds.</returns>
		private static bool TryResolveJournalId(object payload, out int journalId)
		{
			if (payload is JournalDetailRequestPayload requestPayload)
			{
				journalId = requestPayload.JournalId;
				return journalId > 0;
			}

			if (payload is int id)
			{
				journalId = id;
				return journalId > 0;
			}

			if (payload is string raw && int.TryParse(raw, out var parsed))
			{
				journalId = parsed;
				return journalId > 0;
			}

			journalId = 0;
			return false;
		}

		/// <summary>
		/// Publishes standardized request error event.
		/// </summary>
		/// <param name="message">Human-readable error message.</param>
		private static void PublishError(string message)
		{
			EventBus.Publish(JournalEvents.RequestFailed, new JournalErrorPayload
			{
				Message = message
			});
		}

		private static Sprite GetAvatar(string characterName)
		{
			return JournalState.ParentSignals?.GetAvatar?.Invoke(characterName);
		}

		// ==================================================================
		// Playback internal helpers
		// ==================================================================

		/// <summary>
		/// Loads all selected journal details and builds the flattened playback queue.
		/// Journals are ordered from highest id to lowest (bottom-to-top in list).
		/// </summary>
		private static async Task StartPlaybackInternalAsync()
		{
			try
			{
				var selectedIds = new List<int>(JournalState.SelectedJournalIds);
				// Sort descending so bottom journal (highest index in list) plays first.
				selectedIds.Sort((a, b) => b.CompareTo(a));

				// Load details for any uncached journals.
				foreach (var journalId in selectedIds)
				{
					if (!JournalState.CachedDetails.ContainsKey(journalId))
					{
						var detail = await LoadJournalDetailForPlaybackAsync(journalId);
						if (detail != null)
						{
							JournalState.CachedDetails[journalId] = detail;
						}
					}
				}

				// Build flattened queue from all journal messages.
				var queue = new List<JournalPlaybackQueueItem>();
				var queueIndex = 0;
				foreach (var journalId in selectedIds)
				{
					if (!JournalState.CachedDetails.TryGetValue(journalId, out var detail))
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
						queue.Add(new JournalPlaybackQueueItem
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

				if (queue.Count == 0)
				{
					PublishError("No messages found in selected journals.");
					return;
				}

				JournalState.PlaybackQueue = queue;
				JournalState.CurrentPlaybackIndex = 0;
				JournalState.IsPlaying = true;
				JournalState.IsPaused = false;

				EventBus.Publish(JournalEvents.PlaybackStarted, BuildPlaybackStatePayload());

				// Request TTS for first message then publish.
				await RequestAndPublishPlaybackAudioAsync(0);
			}
			catch (Exception exception)
			{
				PublishError("Failed to start playback: " + exception.Message);
			}
		}

		/// <summary>
		/// Loads a single journal detail for playback (does not affect main view state).
		/// </summary>
		/// <param name="journalId">Journal id to load.</param>
		/// <returns>Detail response, or null on failure.</returns>
		private static async Task<JournalDetailResponsePayload> LoadJournalDetailForPlaybackAsync(int journalId)
		{
			try
			{
				var endpoint = BuildJournalDetailEndpoint(journalId);
				var responseJson = await HttpClient.GetTaskAsync(endpoint);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					return null;
				}

				var response = JsonConvert.DeserializeObject<JournalDetailResponsePayload>(responseJson);
				if (response?.Journal == null)
				{
					return null;
				}

				if (response.Messages == null)
				{
					response.Messages = new List<JournalMessagePayload>();
				}

				return response;
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[JournalController] Failed to load journal " + journalId + " for playback: " + exception.Message);
				return null;
			}
		}

		/// <summary>
		/// Requests TTS for the message at the given queue index and publishes
		/// <see cref="JournalEvents.PlaybackMessageChanged"/> with the audio URL.
		/// </summary>
		/// <param name="queueIndex">Index in <see cref="JournalState.PlaybackQueue"/>.</param>
		private static async Task RequestAndPublishPlaybackAudioAsync(int queueIndex)
		{
			var queue = JournalState.PlaybackQueue;
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
					var endpoint = BuildTextToSpeechEndpoint(item.Text, item.Tone, item.CharacterName, false);
					var responseJson = await HttpClient.GetTaskAsync(endpoint);
					if (!string.IsNullOrWhiteSpace(responseJson))
					{
						var ttsResponse = JsonConvert.DeserializeObject<JournalTextToSpeechResponsePayload>(responseJson);
						if (ttsResponse != null && !string.IsNullOrWhiteSpace(ttsResponse.Url))
						{
							audioUrl = ttsResponse.Url;
						}
					}
				}
				catch (Exception exception)
				{
					Debug.LogWarning("[JournalController] TTS request failed for queue item " + queueIndex + ": " + exception.Message);
				}
			}

			// Publish even if audioUrl is null so the View can skip to next.
			EventBus.Publish(JournalEvents.PlaybackMessageChanged, new JournalPlaybackMessageChangedPayload
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
			if (newIndex >= JournalState.PlaybackQueue.Count)
			{
				StopPlaybackInternal();
				return;
			}

			JournalState.CurrentPlaybackIndex = newIndex;
			JournalState.IsPaused = false;
			_ = RequestAndPublishPlaybackAudioAsync(newIndex);
		}

		/// <summary>
		/// Stops playback, resets state, and publishes stopped event.
		/// </summary>
		private static void StopPlaybackInternal()
		{
			var wasFloating = JournalState.IsFloatingMode;
			JournalState.ResetPlaybackState();

			// Exit floating mode if active.
			if (wasFloating)
			{
				JournalState.IsFloatingMode = false;
				EventBus.Publish(JournalEvents.FloatingModeChanged, new JournalFloatingModePayload
				{
					IsFloating = false
				});
			}

			EventBus.Publish(JournalEvents.PlaybackStopped, new JournalPlaybackStatePayload
			{
				IsPlaying = false,
				IsPaused = false,
				CurrentIndex = 0,
				TotalCount = 0
			});
		}

		/// <summary>
		/// Builds a <see cref="JournalPlaybackStatePayload"/> from current state.
		/// </summary>
		/// <returns>Current playback state payload.</returns>
		private static JournalPlaybackStatePayload BuildPlaybackStatePayload()
		{
			return new JournalPlaybackStatePayload
			{
				IsPlaying = JournalState.IsPlaying,
				IsPaused = JournalState.IsPaused,
				CurrentIndex = JournalState.CurrentPlaybackIndex,
				TotalCount = JournalState.PlaybackQueue.Count
			};
		}
	}
}
