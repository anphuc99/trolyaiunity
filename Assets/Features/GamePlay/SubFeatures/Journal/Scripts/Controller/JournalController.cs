using Features.GamePlay.SubFeatures.Journal.Events;
using Features.GamePlay.SubFeatures.Journal.Infrastructure;
using Features.GamePlay.SubFeatures.Journal.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Journal.Model;
using Features.GamePlay.SubFeatures.Journal.Requests;
using Core.Infrastructure.Network;
using Core.Infrastructure.Scenes;
using Core.Infrastructure.State;
using Newtonsoft.Json;
using Share.Utils;
using System;
using System.IO;
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
			JournalState.IsFsrsMode = false;
			JournalState.FsrsReviewingJournalId = null;
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
			// Clear API mode so the next journal session starts fresh (default mode).
			GlobalVariables.Remove(GlobalModes.JournalApiModeKey);
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
		// Playback handlers
		// ==================================================================

		/// <summary>
		/// Saves selected journal IDs to GlobalVariables and loads the JournalOverlay scene.
		/// The actual playback is handled entirely by the JournalOverlay feature.
		/// </summary>
		[Request(JournalRequests.StartPlayback)]
		public static void HandleStartPlayback(JournalStartPlaybackRequestPayload payload)
		{
			if (payload == null || payload.SelectedIds == null || payload.SelectedIds.Count == 0)
			{
				PublishError("No journals selected for playback.");
				return;
			}

			// Copy selected IDs to GlobalVariables so JournalOverlay can read them.
			GlobalVariables.Set(
				SelectedJournalIdsGlobalKey,
				new List<int>(payload.SelectedIds));

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
						Message = PinyinRichTextUtils.BuildInlineRuby(message.Content, message.Pinyin),
						OriginalMessage = message.Content,
						Avatar = GetAvatar(message.CharacterName), // Avatar can be set based on sender or other logic
						Tone = message.Tone,
						Translation = message.Translation,
						Pinyin = message.Pinyin,
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
				var endpoint = BuildTextToSpeechEndpoint(payload.Text, payload.Tone, payload.CharacterName, payload.ForceReload, payload.MessageId);
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

				var settings = Resources.Load<NetworkSettings>("NetworkSettings");
				var baseUrl = AudioUrlUtils.NormalizeServerBaseUrl(settings != null ? settings.BaseUrl : null);
				var resolvedUrl = AudioUrlUtils.ResolveAudioUrl(response.Url, baseUrl);
				if (string.IsNullOrWhiteSpace(resolvedUrl))
				{
					PublishError("Failed to resolve audio URL.");
					return;
				}

				var audioType = AudioUrlUtils.ResolveAudioType(resolvedUrl);
				var clip = await HttpClient.DownloadAudioClipTaskAsync(resolvedUrl, audioType);

				EventBus.Publish(JournalEvents.MessageAudioPlayRequested, new JournalPlayMessageAudioPayload
				{
					MessageId = payload.MessageId,
					MessageIndex = payload.MessageIndex,
					CharacterName = payload.CharacterName,
					Text = payload.Text,
					Tone = payload.Tone,
					AudioUrl = response.Url,
					Clip = clip
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
			if (IsMyLogJournalMode())
			{
				return NetworkEndpoints.MyLogJournals;
			}

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
			if (IsMyLogJournalMode())
			{
				return NetworkEndpoints.MyLogJournals + "/" + journalId;
			}

			return NetworkEndpoints.Journals + "/" + journalId;
		}

		/// <summary>
		/// Builds journal audio download endpoint.
		/// </summary>
		/// <param name="journalId">Journal id.</param>
		/// <returns>Relative endpoint path for audio download.</returns>
		private static string BuildJournalAudioEndpoint(int journalId)
		{
			if (IsMyLogJournalMode())
			{
				return NetworkEndpoints.MyLogJournals + "/" + journalId + "/audio";
			}

			return NetworkEndpoints.Journals + "/" + journalId + "/audio";
		}

		/// <summary>
		/// Builds text-to-speech endpoint with query parameters.
		/// </summary>
		/// <param name="text">Message text.</param>
		/// <param name="tone">Optional tone hint.</param>
		/// <param name="characterName">Character display name.</param>
		/// <param name="forceReload">True to force regeneration on server.</param>
		/// <param name="messageId">Optional message id for persisting missing audio mapping.</param>
		/// <returns>Resolved endpoint path.</returns>
		private static string BuildTextToSpeechEndpoint(string text, string tone, string characterName, bool forceReload, string messageId = null)
		{
			var safeText = string.IsNullOrWhiteSpace(text) ? string.Empty : text;
			var safeTone = string.IsNullOrWhiteSpace(tone) ? "neutral" : tone.Trim();
			var safeName = string.IsNullOrWhiteSpace(characterName) ? string.Empty : characterName.Trim();
			var safeMessageId = string.IsNullOrWhiteSpace(messageId) ? string.Empty : messageId.Trim();
			var endpoint = NetworkEndpoints.TextToSpeech;
			var query = "text=" + Uri.EscapeDataString(safeText)
				+ "&tone=" + Uri.EscapeDataString(safeTone)
				+ "&characterName=" + Uri.EscapeDataString(safeName);

			if (!string.IsNullOrWhiteSpace(safeMessageId))
			{
				query += "&messageId=" + Uri.EscapeDataString(safeMessageId);
			}

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

		// ==================================================================
		// FSRS Journal Review
		// ==================================================================

		/// <summary>
		/// Loads journals due for FSRS spaced-repetition review from the server.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[Request(JournalRequests.LoadDueJournals)]
		public static void HandleLoadDueJournals(object payload)
		{
			_ = LoadDueJournalsInternalAsync();
		}

		/// <summary>
		/// Submits an FSRS review rating for a journal.
		/// </summary>
		/// <param name="payload">Review request payload with journalId and rating.</param>
		[Request(JournalRequests.SubmitJournalReview)]
		public static void HandleSubmitJournalReview(JournalSubmitReviewRequestPayload payload)
		{
			if (payload == null)
			{
				PublishError("Missing journal review payload.");
				return;
			}

			if (payload.JournalId <= 0)
			{
				PublishError("Invalid journal id for review.");
				return;
			}

			if (payload.Rating < 1 || payload.Rating > 4)
			{
				PublishError("Rating must be between 1 and 4.");
				return;
			}

			_ = SubmitJournalReviewInternalAsync(payload);
		}

		// ==================================================================
		// Download Audio handler
		// ==================================================================

		/// <summary>
		/// Handles request to download all audio for a journal as a single MP3.
		/// Publishes AudioDownloadCompleted with the resolved download URL for the view.
		/// </summary>
		/// <param name="payload">Payload containing journal id.</param>
		[Request(JournalRequests.DownloadAudio)]
		public static void HandleDownloadAudio(object payload)
		{
			if (!TryResolveJournalId(payload, out var journalId))
			{
				PublishError("Missing or invalid journal id for audio download.");
				return;
			}

			_ = DownloadAndSaveAudioInternalAsync(journalId);
		}

		/// <summary>
		/// Downloads the combined journal MP3 binary and saves it to persistent storage.
		/// Publishes AudioDownloadCompleted event when finished.
		/// </summary>
		/// <param name="journalId">Journal id to download audio for.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task DownloadAndSaveAudioInternalAsync(int journalId)
		{
			try
			{
				var endpoint = BuildJournalAudioEndpoint(journalId);
				var audioData = await HttpClient.GetBytesTaskAsync(endpoint);
				if (audioData == null || audioData.Length == 0)
				{
					PublishError("Downloaded audio is empty.");
					EventBus.Publish(JournalEvents.AudioDownloadCompleted, new JournalAudioDownloadPayload
					{
						JournalId = journalId,
						Success = false
					});
					return;
				}

				var fileName = "journal_" + journalId + "_audio.mp3";
				var savePath = Path.Combine(Application.persistentDataPath, fileName);
				File.WriteAllBytes(savePath, audioData);
				Debug.Log("[JournalController] Audio saved to: " + savePath);

				EventBus.Publish(JournalEvents.AudioDownloadCompleted, new JournalAudioDownloadPayload
				{
					JournalId = journalId,
					SavePath = savePath,
					Success = true
				});
			}
			catch (Exception exception)
			{
				PublishError("Failed to download audio: " + exception.Message);
				EventBus.Publish(JournalEvents.AudioDownloadCompleted, new JournalAudioDownloadPayload
				{
					JournalId = journalId,
					Success = false
				});
			}
		}

		/// <summary>
		/// Loads due journals from the server and publishes DueJournalsLoaded event.
		/// </summary>
		private static async Task LoadDueJournalsInternalAsync()
		{
			try
			{
				var responseJson = await HttpClient.GetTaskAsync(GetJournalReviewDueEndpoint());
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Empty due journals response from server.");
					return;
				}

				var response = JsonConvert.DeserializeObject<JournalDueListResponsePayload>(responseJson)
					?? new JournalDueListResponsePayload();
				if (response.Journals == null)
				{
					response.Journals = new System.Collections.Generic.List<JournalDueItemPayload>();
				}

				JournalState.IsFsrsMode = true;
				EventBus.Publish(JournalEvents.DueJournalsLoaded, response);
			}
			catch (Exception exception)
			{
				PublishError("Failed to load due journals: " + exception.Message);
			}
		}

		/// <summary>
		/// Posts a journal review rating to the server and publishes ReviewSubmitted event.
		/// </summary>
		/// <param name="payload">Review request payload.</param>
		private static async Task SubmitJournalReviewInternalAsync(JournalSubmitReviewRequestPayload payload)
		{
			try
			{
				var body = new JournalReviewApiRequestBody
				{
					JournalId = payload.JournalId,
					Rating = payload.Rating
				};

				var responseJson = await HttpClient.PostJsonTaskAsync(GetJournalReviewEndpoint(), body);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Empty journal review response from server.");
					return;
				}

				var response = JsonConvert.DeserializeObject<JournalReviewApiResponsePayload>(responseJson);
				if (response == null)
				{
					PublishError("Server returned invalid journal review payload.");
					return;
				}

				EventBus.Publish(JournalEvents.ReviewSubmitted, response);
			}
			catch (Exception exception)
			{
				PublishError("Failed to submit journal review: " + exception.Message);
			}
		}

		private static Sprite GetAvatar(string characterName)
		{
			return JournalState.ParentSignals?.GetAvatar?.Invoke(characterName);
		}

		private static string GetJournalReviewDueEndpoint()
		{
			return IsMyLogJournalMode() ? NetworkEndpoints.MyLogJournalReviewDue : NetworkEndpoints.JournalReviewDue;
		}

		private static string GetJournalReviewEndpoint()
		{
			return IsMyLogJournalMode() ? NetworkEndpoints.MyLogJournalReview : NetworkEndpoints.JournalReview;
		}

		private static bool IsMyLogJournalMode()
		{
			var mode = GlobalVariables.GetOrDefault(GlobalModes.JournalApiModeKey, GlobalModes.ModeDefault);
			return string.Equals(mode, GlobalModes.ModeMyLog, StringComparison.OrdinalIgnoreCase);
		}
	}
}
