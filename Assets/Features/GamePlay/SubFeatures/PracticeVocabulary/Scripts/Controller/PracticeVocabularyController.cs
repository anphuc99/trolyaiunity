using Features.GamePlay.SubFeatures.PracticeVocabulary.Events;
using Features.GamePlay.SubFeatures.PracticeVocabulary.Infrastructure;
using Features.GamePlay.SubFeatures.PracticeVocabulary.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.PracticeVocabulary.Model;
using Features.GamePlay.SubFeatures.PracticeVocabulary.Requests;
using Core.Infrastructure.Network;
using Newtonsoft.Json;
using Share.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.PracticeVocabulary.Controller
{
	/// <summary>
	/// Controller for PracticeVocabulary.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class PracticeVocabularyController
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
		}

		/// <summary>
		/// Installs this subfeature.
		/// </summary>
		public static void Install()
		{
			EventBus.Publish(PracticeVocabularyEvents.Installed, null);
		}

		/// <summary>
		/// Uninstalls this subfeature.
		/// </summary>
		public static void Uninstall()
		{
			EventBus.Publish(PracticeVocabularyEvents.Uninstalled, null);
		}

		/// <summary>
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(PracticeVocabularyParentSignals signals)
		{
			PracticeVocabularyState.ParentSignals = signals;
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(PracticeVocabularyRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(PracticeVocabularyEvents.Echoed, payload);
			PracticeVocabularyState.ParentSignals?.OnEchoed?.Invoke(payload);
		}

		/// <summary>
		/// Loads vocabularies that are currently due for FSRS review.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[Request(PracticeVocabularyRequests.LoadDueReviews)]
		public static void HandleLoadDueReviews(object payload)
		{
			_ = LoadDueReviewsInternalAsync();
		}

		/// <summary>
		/// Submits an FSRS rating for one vocabulary item.
		/// </summary>
		/// <param name="payload">Review payload.</param>
		[Request(PracticeVocabularyRequests.SubmitReview)]
		public static void HandleSubmitReview(PracticeVocabularyReviewRequestPayload payload)
		{
			if (payload == null)
			{
				PublishError("Missing vocabulary review payload.");
				return;
			}

			if (string.IsNullOrWhiteSpace(payload.VocabularyId))
			{
				PublishError("Vocabulary id is required for review.");
				return;
			}

			if (payload.Rating < 1 || payload.Rating > 4)
			{
				PublishError("Rating must be between 1 and 4.");
				return;
			}

			_ = SubmitReviewInternalAsync(payload);
		}

		/// <summary>
		/// Gets all cached character names from parent scope for pronunciation dropdown.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		/// <returns>Distinct non-empty character names.</returns>
		[Request(PracticeVocabularyRequests.GetAllCharacterNames)]
		public static List<string> HandleGetAllCharacterNames(object payload)
		{
			var names = PracticeVocabularyState.ParentSignals?.GetCharacterNames?.Invoke();
			if (names == null || names.Count == 0)
			{
				return new List<string>();
			}

			var normalized = new List<string>();
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			for (var i = 0; i < names.Count; i++)
			{
				var name = names[i];
				if (string.IsNullOrWhiteSpace(name))
				{
					continue;
				}

				var safeName = name.Trim();
				if (!seen.Add(safeName))
				{
					continue;
				}

				normalized.Add(safeName);
			}

			normalized.Sort(StringComparer.OrdinalIgnoreCase);
			return normalized;
		}

		/// <summary>
		/// Handles vocab audio playback requests from the view.
		/// </summary>
		/// <param name="payload">Audio request payload.</param>
		[Request(PracticeVocabularyRequests.PlayVocabularyAudio)]
		public static void HandlePlayVocabularyAudio(PracticeVocabularyPlayAudioRequestPayload payload)
		{
			if (payload == null || string.IsNullOrWhiteSpace(payload.Text))
			{
				PublishError("Missing vocabulary content for audio playback.");
				return;
			}

			_ = PlayVocabularyAudioInternalAsync(payload);
		}

		/// <summary>
		/// Ignores a vocabulary item so it never appears in reviews again.
		/// </summary>
		/// <param name="payload">Ignore request payload.</param>
		[Request(PracticeVocabularyRequests.IgnoreVocabulary)]
		public static void HandleIgnoreVocabulary(PracticeVocabularyIgnoreRequestPayload payload)
		{
			if (payload == null)
			{
				PublishError("Missing vocabulary ignore payload.");
				return;
			}

			if (string.IsNullOrWhiteSpace(payload.VocabularyId))
			{
				PublishError("Vocabulary id is required to ignore.");
				return;
			}

			_ = IgnoreVocabularyInternalAsync(payload);
		}

		private sealed class ResolvedTtsPayload
		{
			public string Url;
			public string Text;
			public string Pinyin;
			public bool Rewritten;
		}

		/// <summary>
		/// Calls server due endpoint and publishes parsed response.
		/// </summary>
		/// <returns>Awaitable task.</returns>
		private static async Task LoadDueReviewsInternalAsync()
		{
			try
			{
				var responseJson = await HttpClient.GetTaskAsync(NetworkEndpoints.VocabularyDue);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Empty due vocabulary response from server.");
					return;
				}

				var response = JsonConvert.DeserializeObject<PracticeVocabularyDueListResponsePayload>(responseJson)
					?? new PracticeVocabularyDueListResponsePayload();

				if (response.Vocabularies == null)
				{
					response.Vocabularies = new List<PracticeVocabularyItemPayload>();
				}

				if (response.Total < response.Vocabularies.Count)
				{
					response.Total = response.Vocabularies.Count;
				}

				EventBus.Publish(PracticeVocabularyEvents.DueReviewsLoaded, response);
			}
			catch (Exception exception)
			{
				PublishError("Failed to load due vocabularies: " + exception.Message);
			}
		}

		/// <summary>
		/// Calls server review endpoint and publishes completion payload.
		/// </summary>
		/// <param name="payload">Validated review payload.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task SubmitReviewInternalAsync(PracticeVocabularyReviewRequestPayload payload)
		{
			try
			{
				var safeVocabularyId = payload.VocabularyId.Trim();
				var endpoint = NetworkEndpoints.VocabularyReview + "/" + Uri.EscapeDataString(safeVocabularyId) + "/review";
				var body = new { rating = payload.Rating };

				var responseJson = await HttpClient.PostJsonTaskAsync(endpoint, body);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Empty vocabulary review response from server.");
					return;
				}

				var review = JsonConvert.DeserializeObject<PracticeVocabularyReviewPayload>(responseJson);
				if (review == null)
				{
					PublishError("Failed to parse vocabulary review response.");
					return;
				}

				EventBus.Publish(PracticeVocabularyEvents.ReviewSubmitted, new PracticeVocabularyReviewSubmittedPayload
				{
					VocabularyId = safeVocabularyId,
					Rating = payload.Rating,
					Review = review
				});
			}
			catch (Exception exception)
			{
				PublishError("Failed to submit vocabulary review: " + exception.Message);
			}
		}

		/// <summary>
		/// Calls server ignore endpoint and publishes completion event.
		/// </summary>
		/// <param name="payload">Validated ignore payload.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task IgnoreVocabularyInternalAsync(PracticeVocabularyIgnoreRequestPayload payload)
		{
			try
			{
				var safeVocabularyId = payload.VocabularyId.Trim();
				var endpoint = NetworkEndpoints.VocabularyIgnore + "/" + Uri.EscapeDataString(safeVocabularyId) + "/ignore";
				var body = new { };

				var responseJson = await HttpClient.PutJsonTaskAsync(endpoint, body);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Empty vocabulary ignore response from server.");
					return;
				}

				EventBus.Publish(PracticeVocabularyEvents.VocabularyIgnored, new PracticeVocabularyIgnoredPayload
				{
					VocabularyId = safeVocabularyId
				});
			}
			catch (Exception exception)
			{
				PublishError("Failed to ignore vocabulary: " + exception.Message);
			}
		}

		/// <summary>
		/// Resolves vocabulary TTS URL, downloads audio clip and publishes playback event.
		/// </summary>
		/// <param name="payload">Audio request payload.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task PlayVocabularyAudioInternalAsync(PracticeVocabularyPlayAudioRequestPayload payload)
		{
			try
			{
				var safeText = payload.Text.Trim();
				var characterName = string.IsNullOrWhiteSpace(payload.CharacterName)
					? string.Empty
					: payload.CharacterName.Trim();

				var voiceName = string.IsNullOrWhiteSpace(characterName)
					? null
					: PracticeVocabularyState.ParentSignals?.GetCharacterVoiceNameByName?.Invoke(characterName);

				var pitch = string.IsNullOrWhiteSpace(characterName)
					? null
					: PracticeVocabularyState.ParentSignals?.GetCharacterPitchByName?.Invoke(characterName);

				var speakingRate = string.IsNullOrWhiteSpace(characterName)
					? null
					: PracticeVocabularyState.ParentSignals?.GetCharacterSpeakingRateByName?.Invoke(characterName);

				var resolvedTts = await ResolveTtsAudioUrlAsync(
					safeText,
					payload.Tone,
					characterName,
					payload.ForceReload);
				var audioUrl = resolvedTts != null ? resolvedTts.Url : null;

				AudioClip audioClip = null;
				if (!string.IsNullOrWhiteSpace(audioUrl))
				{
					var audioType = AudioUrlUtils.ResolveAudioType(audioUrl);
					audioClip = await HttpClient.DownloadAudioClipTaskAsync(audioUrl, audioType);
				}

				EventBus.Publish(PracticeVocabularyEvents.VocabularyAudioPlayRequested, new PracticeVocabularyPlayAudioPayload
				{
					CharacterName = characterName,
					Text = safeText,
					UpdatedText = resolvedTts != null ? resolvedTts.Text : null,
					UpdatedPinyin = resolvedTts != null ? resolvedTts.Pinyin : null,
					Tone = payload.Tone,
					VoiceName = voiceName,
					Pitch = pitch,
					SpeakingRate = speakingRate,
					AudioUrl = audioUrl,
					AudioClip = audioClip,
					ForceReload = payload.ForceReload,
				});
			}
			catch (Exception exception)
			{
				PublishError("Failed to resolve vocabulary audio: " + exception.Message);
			}
		}

		/// <summary>
		/// Calls the TTS endpoint and returns resolved audio URL plus rewrite metadata.
		/// </summary>
		/// <param name="text">Text to synthesize.</param>
		/// <param name="tone">Tone hint.</param>
		/// <param name="characterName">Character name.</param>
		/// <param name="forceReload">Whether to force regeneration.</param>
		/// <returns>Resolved TTS payload, or null on failure.</returns>
		private static async Task<ResolvedTtsPayload> ResolveTtsAudioUrlAsync(
			string text,
			string tone,
			string characterName,
			bool forceReload = false)
		{
			try
			{
				var query = AudioUrlUtils.BuildTextToSpeechQuery(text, tone, characterName, forceReload);
				var endpoint = NetworkEndpoints.TextToSpeech + query;
				var responseJson = await HttpClient.GetTaskAsync(endpoint);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					return null;
				}

				var ttsResponse = JsonConvert.DeserializeObject<PracticeVocabularyTextToSpeechResponsePayload>(responseJson);
				var rawUrl = ttsResponse?.Url;
				if (string.IsNullOrWhiteSpace(rawUrl))
				{
					return null;
				}

				var settings = Resources.Load<NetworkSettings>("NetworkSettings");
				var baseUrl = AudioUrlUtils.NormalizeServerBaseUrl(settings != null ? settings.BaseUrl : null);
				return new ResolvedTtsPayload
				{
					Url = AudioUrlUtils.ResolveAudioUrl(rawUrl, baseUrl),
					Text = ttsResponse.Text,
					Pinyin = ttsResponse.Pinyin,
					Rewritten = ttsResponse.Rewritten,
				};
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[PracticeVocabularyController] TTS resolution failed: " + exception.Message);
				return null;
			}
		}

		/// <summary>
		/// Publishes controller-level request failure.
		/// </summary>
		/// <param name="message">Error message.</param>
		private static void PublishError(string message)
		{
			EventBus.Publish(PracticeVocabularyEvents.RequestFailed, new PracticeVocabularyErrorPayload
			{
				Message = message
			});
		}
	}
}
