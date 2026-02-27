using Features.GamePlay.SubFeatures.Journal.Events;
using Features.GamePlay.SubFeatures.Journal.Infrastructure;
using Features.GamePlay.SubFeatures.Journal.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Journal.Model;
using Features.GamePlay.SubFeatures.Journal.Requests;
using Core.Infrastructure.Network;
using Core.Infrastructure.Scenes;
using Core.Infrastructure.State;
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
		/// GlobalVariables key shared with JournalOverlay feature for passing selected journal IDs.
		/// Must match <c>Features.JournalOverlay.Model.JournalOverlayGlobalKeys.SelectedJournalIds</c>.
		/// </summary>
		private const string SelectedJournalIdsGlobalKey = "global.journal.overlay.selected.ids";

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
		/// Saves selected journal IDs to GlobalVariables and loads the JournalOverlay scene.
		/// The actual playback is handled entirely by the JournalOverlay feature.
		/// </summary>
		[Request(JournalRequests.StartPlayback)]
		public static void HandleStartPlayback(object payload)
		{
			if (JournalState.SelectedJournalIds.Count == 0)
			{
				PublishError("No journals selected for playback.");
				return;
			}

			// Copy selected IDs to GlobalVariables so JournalOverlay can read them.
			GlobalVariables.Set(
				SelectedJournalIdsGlobalKey,
				new List<int>(JournalState.SelectedJournalIds));

			// Load the JournalOverlay scene additively.
			LoadScene.ByScope(
				Core.Infrastructure.Attributes.ControllerScopeKey.JournalOverlayGameplay,
				UnityEngine.SceneManagement.LoadSceneMode.Additive);
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
	}
}
