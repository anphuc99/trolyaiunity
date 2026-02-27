using Core.Infrastructure.Views;
using Core.Infrastructure.Network;
using Features.GamePlay.SubFeatures.Journal.Events;
using Features.GamePlay.SubFeatures.Journal.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Journal.Model;
using Features.GamePlay.SubFeatures.Journal.Requests;
using Share.Components;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

namespace Features.GamePlay.SubFeatures.Journal.View
{
	/// <summary>
	/// View for Journal.
	/// Handles journal list display, detail chat view, multi-select,
	/// sequential auto-play with overlay floating window support.
	/// </summary>
	public sealed class JournalView : BaseView
	{
		[Header("List view")]
		[SerializeField]
		private RectTransform _listContent;

		[SerializeField]
		private GameObject _listItemTemplate;

		[SerializeField]
		private GameObject _listRoot;

		[Header("Detail view")]
		[SerializeField]
		private JournalHistoryChat _chatVariantRoot;

		[Header("Options")]
		[SerializeField]
		private bool _loadOnInstall = true;

		[Header("Audio")]
		[SerializeField]
		private AudioSource _voiceAudioSource;

		[Header("Playback controls")]
		[SerializeField] private GameObject _playAllButton;
		[SerializeField] private GameObject _playbackControlPanel;
		[SerializeField] private Button _playbackPauseResumeButton;
		[SerializeField] private Button _playbackNextButton;
		[SerializeField] private Button _playbackPreviousButton;
		[SerializeField] private Button _playbackStopButton;
		[SerializeField] private Button _playbackFloatingButton;
		[SerializeField] private TMP_Text _playbackProgressText;
		[SerializeField] private GameObject _playbackPlayIcon;
		[SerializeField] private GameObject _playbackPauseIcon;

		[Header("Overlay")]
		[SerializeField] private JournalOverlayManager _overlayManager;

		private readonly List<GameObject> _spawnedListItems = new List<GameObject>();
		private readonly HashSet<int> _reloadingTtsMessageIndices = new HashSet<int>();
		private readonly Dictionary<int, Toggle> _selectionToggles = new Dictionary<int, Toggle>();
		private NetworkSettings _networkSettings;
		private Coroutine _autoPlayCoroutine;
		private bool _isAutoPlaying;

		/// <summary>
		/// Requests journal list from API.
		/// </summary>
		public void LoadJournals()
		{
			SendRequest(JournalRequests.LoadJournals, new JournalListRequestPayload());
		}

		/// <summary>
		/// Requests switching back to list mode.
		/// </summary>
		public void ShowJournalList()
		{
			SendRequest(JournalRequests.ShowJournalList);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(JournalEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[JournalView] Echoed: " + payload, this);
		}

		/// <summary>
		/// Renders journal list when loaded from API.
		/// </summary>
		/// <param name="payload">List payload from controller.</param>
		[OnEvent(JournalEvents.JournalsLoaded)]
		private void OnJournalsLoaded(object payload)
		{
			if (payload is not JournalListResponsePayload response)
			{
				return;
			}

			EnsureBindings();
			RenderJournalList(response.Journals);
		}

		/// <summary>
		/// Renders journal detail when one entry is selected.
		/// </summary>
		/// <param name="payload">Detail payload from controller.</param>
		[OnEvent(JournalEvents.JournalDetailLoaded)]
		private void OnJournalDetailLoaded(object payload)
		{
			if (payload is not List<MessageBubbleData> response)
			{
				return;
			}

			EnsureBindings();
			RenderJournalDetail(response);
		}

		/// <summary>
		/// Toggles list/detail visibility based on controller mode event.
		/// </summary>
		/// <param name="payload">View mode payload.</param>
		[OnEvent(JournalEvents.ViewModeChanged)]
		private void OnViewModeChanged(object payload)
		{
			if (payload is not JournalViewModePayload viewMode)
			{
				return;
			}

			EnsureBindings();
			SetMode(viewMode.ShowDetail);
		}

		/// <summary>
		/// Logs API failures for troubleshooting.
		/// </summary>
		/// <param name="payload">Error payload from controller.</param>
		[OnEvent(JournalEvents.RequestFailed)]
		private void OnRequestFailed(object payload)
		{
			var message = (payload as JournalErrorPayload)?.Message;
			if (string.IsNullOrWhiteSpace(message))
			{
				message = "Journal request failed.";
			}

			Debug.LogError("[JournalView] " + message, this);
		}

		/// <summary>
		/// Plays journal message audio when controller provides audio URL.
		/// </summary>
		/// <param name="payload">Audio playback payload.</param>
		[OnEvent(JournalEvents.MessageAudioPlayRequested)]
		private void OnMessageAudioPlayRequested(object payload)
		{
			if (payload is not JournalPlayMessageAudioPayload audioPayload)
			{
				return;
			}

			if (string.IsNullOrWhiteSpace(audioPayload.AudioUrl))
			{
				ClearReloadingState(audioPayload.MessageIndex);
				Debug.LogWarning("[JournalView] Missing audio URL for playback.", this);
				return;
			}

			EnsureAudioSource();
			StartCoroutine(PlayJournalAudioAsync(audioPayload));
		}

		/// <summary>
		/// Shows this subfeature view when its controller is installed.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(JournalEvents.Installed)]
		private void OnInstalled(object payload)
		{
			EnsureBindings();
			EnsurePlaybackBindings();
			SetMode(false);
			SetPlaybackControlsVisible(false);
			gameObject.SetActive(true);

			if (_loadOnInstall)
			{
				LoadJournals();
			}
		}

		/// <summary>
		/// Hides this subfeature view when its controller is uninstalled.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(JournalEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			StopAutoPlayCoroutine();
			StopAllCoroutines();
			_reloadingTtsMessageIndices.Clear();
			if (_voiceAudioSource != null)
			{
				_voiceAudioSource.Stop();
			}

			// Restore window if floating.
			if (_overlayManager != null)
			{
				_overlayManager.Hide();
			}

			SetPlaybackControlsVisible(false);
			gameObject.SetActive(false);
		}

		/// <summary>
		/// Resolves optional UI references from hierarchy when not assigned in inspector.
		/// </summary>
		private void EnsureBindings()
		{
			if (_listRoot == null)
			{
				var scrollTransform = transform.Find("Scroll View");
				if (scrollTransform != null)
				{
					_listRoot = scrollTransform.gameObject;
				}
			}

			if (_listContent == null)
			{
				var contentTransform = transform.Find("Scroll View/Viewport/Content");
				if (contentTransform != null)
				{
					_listContent = contentTransform as RectTransform;
				}
			}

			if (_listItemTemplate == null && _listContent != null && _listContent.childCount > 0)
			{
				_listItemTemplate = _listContent.GetChild(0).gameObject;
			}

			if (_chatVariantRoot != null)
			{
				_chatVariantRoot.callback = Callback;
				_chatVariantRoot.OnSpeakerClicked = HandleMessageSpeakerClicked;
				_chatVariantRoot.OnSpeakerLongPressed = HandleMessageSpeakerLongPressed;
			}
		}

		/// <summary>
		/// Sets journal UI mode.
		/// </summary>
		/// <param name="showDetail">True to show detail mode.</param>
		private void SetMode(bool showDetail)
		{
			if (_listRoot != null)
			{
				_listRoot.SetActive(!showDetail);
			}

			if (_chatVariantRoot != null)
			{
				_chatVariantRoot.gameObject.SetActive(showDetail);
			}
		}

		/// <summary>
		/// Renders journal list items with selection toggles for multi-select.
		/// </summary>
		/// <param name="journals">Journal summary items.</param>
		private void RenderJournalList(List<JournalListItemPayload> journals)
		{
			ClearSpawnedListItems();
			_selectionToggles.Clear();

			if (_listContent == null || _listItemTemplate == null)
			{
				return;
			}

			_listItemTemplate.SetActive(false);
			if (journals == null || journals.Count == 0)
			{
				UpdatePlayAllButton(0);
				return;
			}

			for (var i = 0; i < journals.Count; i++)
			{
				var journal = journals[i];
				if (journal == null)
				{
					continue;
				}

				var instance = Instantiate(_listItemTemplate, _listContent);
				instance.name = "JournalItem-" + journal.Id;
				instance.SetActive(true);
				_spawnedListItems.Add(instance);

				var textComponent = instance.GetComponentInChildren<TMP_Text>(true);
				if (textComponent != null)
				{
					textComponent.text = BuildJournalListLabel(journal);
				}

				var button = instance.GetComponent<Button>();
				if (button != null)
				{
					var journalId = journal.Id;
					button.onClick.RemoveAllListeners();
					button.onClick.AddListener(() =>
					{
						SendRequest(JournalRequests.LoadJournalDetail, new JournalDetailRequestPayload
						{
							JournalId = journalId
						});
					});
				}

				// Add or find a selection toggle for multi-select.
				var toggle = instance.GetComponentInChildren<Toggle>(true);
				if (toggle != null)
				{
					var journalId = journal.Id;
					var isSelected = JournalState.SelectedJournalIds.Contains(journalId);
					toggle.SetIsOnWithoutNotify(isSelected);
					toggle.onValueChanged.RemoveAllListeners();
					toggle.onValueChanged.AddListener((_) =>
					{
						SendRequest(JournalRequests.ToggleJournalSelection, new JournalToggleSelectionPayload
						{
							JournalId = journalId
						});
					});
					_selectionToggles[journalId] = toggle;
				}
			}

			UpdatePlayAllButton(JournalState.SelectedJournalIds.Count);
		}

		/// <summary>
		/// Renders detail text for selected journal chat history.
		/// </summary>
		/// <param name="payload">Journal detail payload.</param>
		private void RenderJournalDetail(List<MessageBubbleData> messageBubbleData)
		{
			_chatVariantRoot.SetChatHistory(messageBubbleData);
		}

		private void HandleMessageSpeakerClicked(MessageBubbleData messageData)
		{
			if (messageData == null || messageData.Type != MessageBubbleType.Character)
			{
				return;
			}

			if (_reloadingTtsMessageIndices.Contains(messageData.MessageIndex) || messageData.IsTtsReloading)
			{
				return;
			}

			var text = string.IsNullOrWhiteSpace(messageData.OriginalMessage) ? messageData.Message : messageData.OriginalMessage;
			if (string.IsNullOrWhiteSpace(text))
			{
				return;
			}

			SendRequest(JournalRequests.PlayMessageAudio, new JournalPlayMessageAudioRequestPayload
			{
				MessageId = messageData.MessageId,
				MessageIndex = messageData.MessageIndex,
				CharacterName = messageData.SenderName,
				Text = text,
				Tone = messageData.Tone,
				ForceReload = false
			});
		}

		private void HandleMessageSpeakerLongPressed(MessageBubbleData messageData)
		{
			if (_chatVariantRoot == null || messageData == null || messageData.Type != MessageBubbleType.Character)
			{
				return;
			}

			var messageIndex = messageData.MessageIndex;
			if (messageIndex < 0 || _reloadingTtsMessageIndices.Contains(messageIndex))
			{
				return;
			}

			var text = string.IsNullOrWhiteSpace(messageData.OriginalMessage) ? messageData.Message : messageData.OriginalMessage;
			if (string.IsNullOrWhiteSpace(text))
			{
				return;
			}

			_reloadingTtsMessageIndices.Add(messageIndex);
			_chatVariantRoot.SetMessageTtsReloading(messageIndex, true);

			SendRequest(JournalRequests.PlayMessageAudio, new JournalPlayMessageAudioRequestPayload
			{
				MessageId = messageData.MessageId,
				MessageIndex = messageIndex,
				CharacterName = messageData.SenderName,
				Text = text,
				Tone = messageData.Tone,
				ForceReload = true
			});
		}

		private IEnumerator PlayJournalAudioAsync(JournalPlayMessageAudioPayload payload)
		{
			var resolvedUrl = ResolveAudioUrl(payload.AudioUrl);
			if (string.IsNullOrWhiteSpace(resolvedUrl))
			{
				ClearReloadingState(payload.MessageIndex);
				yield break;
			}

			using var audioRequest = UnityWebRequestMultimedia.GetAudioClip(resolvedUrl, ResolveAudioType(resolvedUrl));
			yield return audioRequest.SendWebRequest();

			if (audioRequest.result != UnityWebRequest.Result.Success)
			{
				ClearReloadingState(payload.MessageIndex);
				Debug.LogWarning("[JournalView] Failed to download audio: " + audioRequest.error, this);
				yield break;
			}

			var clip = DownloadHandlerAudioClip.GetContent(audioRequest);
			if (clip == null)
			{
				ClearReloadingState(payload.MessageIndex);
				yield break;
			}

			EnsureAudioSource();
			_voiceAudioSource.Stop();
			_voiceAudioSource.clip = clip;
			_voiceAudioSource.Play();
			while (_voiceAudioSource.isPlaying)
			{
				yield return null;
			}

			ClearReloadingState(payload.MessageIndex);
		}

		private void ClearReloadingState(int messageIndex)
		{
			if (messageIndex < 0)
			{
				return;
			}

			if (_reloadingTtsMessageIndices.Remove(messageIndex) && _chatVariantRoot != null)
			{
				_chatVariantRoot.SetMessageTtsReloading(messageIndex, false);
			}
		}

		private void EnsureAudioSource()
		{
			if (_voiceAudioSource != null)
			{
				return;
			}

			_voiceAudioSource = GetComponent<AudioSource>();
			if (_voiceAudioSource == null)
			{
				_voiceAudioSource = gameObject.AddComponent<AudioSource>();
			}
		}

		private string ResolveAudioUrl(string audioUrl)
		{
			if (string.IsNullOrWhiteSpace(audioUrl))
			{
				return null;
			}

			if (Uri.TryCreate(audioUrl, UriKind.Absolute, out var absoluteUri))
			{
				return absoluteUri.ToString();
			}

			var baseUrl = NormalizeServerBaseUrl(GetServerBaseUrl());
			if (string.IsNullOrWhiteSpace(baseUrl))
			{
				return null;
			}

			if (!audioUrl.StartsWith("/", StringComparison.Ordinal))
			{
				audioUrl = "/" + audioUrl;
			}

			return baseUrl + audioUrl;
		}

		private static AudioType ResolveAudioType(string audioUrl)
		{
			if (string.IsNullOrWhiteSpace(audioUrl))
			{
				return AudioType.MPEG;
			}

			if (audioUrl.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
			{
				return AudioType.WAV;
			}

			if (audioUrl.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
			{
				return AudioType.OGGVORBIS;
			}

			return AudioType.MPEG;
		}

		private string GetServerBaseUrl()
		{
			if (_networkSettings == null)
			{
				_networkSettings = Resources.Load<NetworkSettings>("NetworkSettings");
			}

			return _networkSettings != null ? _networkSettings.BaseUrl : null;
		}

		private static string NormalizeServerBaseUrl(string baseUrl)
		{
			if (string.IsNullOrWhiteSpace(baseUrl))
			{
				return null;
			}

			var normalized = baseUrl.Trim().TrimEnd('/');
			if (normalized.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
			{
				normalized = normalized.Substring(0, normalized.Length - 4);
			}

			return normalized;
		}

		/// <summary>
		/// Clears instantiated list item clones.
		/// </summary>
		private void ClearSpawnedListItems()
		{
			for (var i = 0; i < _spawnedListItems.Count; i++)
			{
				if (_spawnedListItems[i] != null)
				{
					Destroy(_spawnedListItems[i]);
				}
			}

			_spawnedListItems.Clear();
		}

		/// <summary>
		/// Builds one-line text for journal summary item.
		/// </summary>
		/// <param name="journal">Journal item payload.</param>
		/// <returns>Display label for list item.</returns>
		private static string BuildJournalListLabel(JournalListItemPayload journal)
		{
			var summary = string.IsNullOrWhiteSpace(journal.Summary) ? "(Không có tóm tắt)" : journal.Summary.Trim();
			if (DateTime.TryParse(journal.CreatedAt, out var createdAt))
			{
				return createdAt.ToString("dd/MM/yyyy HH:mm") + " - " + summary;
			}

			return summary;
		}

		private void Callback()
		{
			_chatVariantRoot.gameObject.SetActive(false);
			_listRoot.SetActive(true);
		}

		// ==================================================================
		// Selection event handlers
		// ==================================================================

		/// <summary>
		/// Updates toggle visuals when the controller publishes selection changes.
		/// Also toggles the Play All button visibility.
		/// </summary>
		/// <param name="payload">Selection changed payload.</param>
		[OnEvent(JournalEvents.SelectionChanged)]
		private void OnSelectionChanged(object payload)
		{
			if (payload is not JournalSelectionChangedPayload selectionPayload)
			{
				return;
			}

			var ids = selectionPayload.SelectedIds ?? new HashSet<int>();
			foreach (var kvp in _selectionToggles)
			{
				kvp.Value.SetIsOnWithoutNotify(ids.Contains(kvp.Key));
			}

			UpdatePlayAllButton(ids.Count);
		}

		// ==================================================================
		// Playback event handlers
		// ==================================================================

		/// <summary>
		/// Handles playback started — shows controls.
		/// </summary>
		/// <param name="payload">Playback state payload.</param>
		[OnEvent(JournalEvents.PlaybackStarted)]
		private void OnPlaybackStarted(object payload)
		{
			SetPlaybackControlsVisible(true);
			UpdatePlaybackPauseIcon(false);
		}

		/// <summary>
		/// Handles playback stopped — hides controls, stops audio.
		/// </summary>
		/// <param name="payload">Playback state payload.</param>
		[OnEvent(JournalEvents.PlaybackStopped)]
		private void OnPlaybackStopped(object payload)
		{
			StopAutoPlayCoroutine();
			if (_voiceAudioSource != null)
			{
				_voiceAudioSource.Stop();
			}

			SetPlaybackControlsVisible(false);
			UpdatePlaybackProgress(0, 0);
		}

		/// <summary>
		/// Handles playback paused — pauses audio source.
		/// </summary>
		/// <param name="payload">Playback state payload.</param>
		[OnEvent(JournalEvents.PlaybackPaused)]
		private void OnPlaybackPaused(object payload)
		{
			if (_voiceAudioSource != null)
			{
				_voiceAudioSource.Pause();
			}

			UpdatePlaybackPauseIcon(true);
			if (_overlayManager != null)
			{
				_overlayManager.SetPausedState(true);
			}
		}

		/// <summary>
		/// Handles playback resumed — unpauses audio source.
		/// </summary>
		/// <param name="payload">Playback state payload.</param>
		[OnEvent(JournalEvents.PlaybackResumed)]
		private void OnPlaybackResumed(object payload)
		{
			if (_voiceAudioSource != null)
			{
				_voiceAudioSource.UnPause();
			}

			UpdatePlaybackPauseIcon(false);
			if (_overlayManager != null)
			{
				_overlayManager.SetPausedState(false);
			}
		}

		/// <summary>
		/// Handles the current playback message changing — downloads and plays audio,
		/// updates progress, and notifies overlay.
		/// </summary>
		/// <param name="payload">Playback message changed payload.</param>
		[OnEvent(JournalEvents.PlaybackMessageChanged)]
		private void OnPlaybackMessageChanged(object payload)
		{
			if (payload is not JournalPlaybackMessageChangedPayload messagePayload)
			{
				return;
			}

			UpdatePlaybackProgress(messagePayload.CurrentIndex, messagePayload.TotalCount);

			// Update overlay if visible.
			if (_overlayManager != null)
			{
				_overlayManager.UpdateCurrentMessage(messagePayload);
			}

			// Stop any previous auto-play coroutine and start a new one.
			StopAutoPlayCoroutine();
			_autoPlayCoroutine = StartCoroutine(AutoPlayMessageAsync(messagePayload));
		}

		/// <summary>
		/// Handles floating mode being toggled.
		/// </summary>
		/// <param name="payload">Floating mode payload.</param>
		[OnEvent(JournalEvents.FloatingModeChanged)]
		private void OnFloatingModeChanged(object payload)
		{
			if (payload is not JournalFloatingModePayload floatingPayload)
			{
				return;
			}

			if (_overlayManager == null)
			{
				return;
			}

			if (floatingPayload.IsFloating)
			{
				_overlayManager.Show();
			}
			else
			{
				_overlayManager.Hide();
			}
		}

		// ==================================================================
		// Auto-play coroutine
		// ==================================================================

		/// <summary>
		/// Downloads and plays audio for a single playback queue message, then
		/// sends <see cref="JournalRequests.AdvancePlayback"/> to move to the next.
		/// </summary>
		/// <param name="messagePayload">Current message data with audio URL.</param>
		private IEnumerator AutoPlayMessageAsync(JournalPlaybackMessageChangedPayload messagePayload)
		{
			_isAutoPlaying = true;

			if (string.IsNullOrWhiteSpace(messagePayload.AudioUrl))
			{
				// No audio available — skip to next immediately.
				_isAutoPlaying = false;
				SendRequest(JournalRequests.AdvancePlayback);
				yield break;
			}

			var resolvedUrl = ResolveAudioUrl(messagePayload.AudioUrl);
			if (string.IsNullOrWhiteSpace(resolvedUrl))
			{
				_isAutoPlaying = false;
				SendRequest(JournalRequests.AdvancePlayback);
				yield break;
			}

			using var audioRequest = UnityWebRequestMultimedia.GetAudioClip(resolvedUrl, ResolveAudioType(resolvedUrl));
			yield return audioRequest.SendWebRequest();

			if (audioRequest.result != UnityWebRequest.Result.Success)
			{
				Debug.LogWarning("[JournalView] Auto-play audio download failed: " + audioRequest.error, this);
				_isAutoPlaying = false;
				SendRequest(JournalRequests.AdvancePlayback);
				yield break;
			}

			var clip = DownloadHandlerAudioClip.GetContent(audioRequest);
			if (clip == null)
			{
				_isAutoPlaying = false;
				SendRequest(JournalRequests.AdvancePlayback);
				yield break;
			}

			EnsureAudioSource();
			_voiceAudioSource.Stop();
			_voiceAudioSource.clip = clip;
			_voiceAudioSource.Play();

			while (_voiceAudioSource.isPlaying || JournalState.IsPaused)
			{
				yield return null;
			}

			_isAutoPlaying = false;
			SendRequest(JournalRequests.AdvancePlayback);
		}

		/// <summary>
		/// Stops and clears the auto-play coroutine if running.
		/// </summary>
		private void StopAutoPlayCoroutine()
		{
			if (_autoPlayCoroutine != null)
			{
				StopCoroutine(_autoPlayCoroutine);
				_autoPlayCoroutine = null;
			}
			_isAutoPlaying = false;
		}

		// ==================================================================
		// Playback UI helpers
		// ==================================================================

		/// <summary>
		/// Wires playback control buttons and overlay callbacks.
		/// </summary>
		private void EnsurePlaybackBindings()
		{
			if (_playAllButton != null)
			{
				var btn = _playAllButton.GetComponent<Button>();
				if (btn != null)
				{
					btn.onClick.RemoveAllListeners();
					btn.onClick.AddListener(() => SendRequest(JournalRequests.StartPlayback));
				}
			}

			if (_playbackPauseResumeButton != null)
			{
				_playbackPauseResumeButton.onClick.RemoveAllListeners();
				_playbackPauseResumeButton.onClick.AddListener(HandlePlaybackPauseResumeClicked);
			}

			if (_playbackNextButton != null)
			{
				_playbackNextButton.onClick.RemoveAllListeners();
				_playbackNextButton.onClick.AddListener(() => SendRequest(JournalRequests.NextMessage));
			}

			if (_playbackPreviousButton != null)
			{
				_playbackPreviousButton.onClick.RemoveAllListeners();
				_playbackPreviousButton.onClick.AddListener(() => SendRequest(JournalRequests.PreviousMessage));
			}

			if (_playbackStopButton != null)
			{
				_playbackStopButton.onClick.RemoveAllListeners();
				_playbackStopButton.onClick.AddListener(() => SendRequest(JournalRequests.StopPlayback));
			}

			if (_playbackFloatingButton != null)
			{
				_playbackFloatingButton.onClick.RemoveAllListeners();
				_playbackFloatingButton.onClick.AddListener(() => SendRequest(JournalRequests.ToggleFloatingMode));
			}

			// Wire overlay manager callbacks.
			if (_overlayManager != null)
			{
				_overlayManager.OnPreviousClicked = () => SendRequest(JournalRequests.PreviousMessage);
				_overlayManager.OnNextClicked = () => SendRequest(JournalRequests.NextMessage);
				_overlayManager.OnStopClicked = () => SendRequest(JournalRequests.StopPlayback);
				_overlayManager.OnCloseClicked = () => SendRequest(JournalRequests.ToggleFloatingMode);
				_overlayManager.OnPlayPauseClicked = HandlePlaybackPauseResumeClicked;
			}
		}

		/// <summary>
		/// Toggles between pause and resume based on current state.
		/// </summary>
		private void HandlePlaybackPauseResumeClicked()
		{
			if (JournalState.IsPaused)
			{
				SendRequest(JournalRequests.ResumePlayback);
			}
			else
			{
				SendRequest(JournalRequests.PausePlayback);
			}
		}

		/// <summary>
		/// Shows or hides the playback control panel.
		/// </summary>
		/// <param name="visible">True to show controls.</param>
		private void SetPlaybackControlsVisible(bool visible)
		{
			if (_playbackControlPanel != null)
			{
				_playbackControlPanel.SetActive(visible);
			}
		}

		/// <summary>
		/// Updates the play/pause icon in playback controls.
		/// </summary>
		/// <param name="isPaused">True when paused (show play icon).</param>
		private void UpdatePlaybackPauseIcon(bool isPaused)
		{
			if (_playbackPlayIcon != null)
			{
				_playbackPlayIcon.SetActive(isPaused);
			}

			if (_playbackPauseIcon != null)
			{
				_playbackPauseIcon.SetActive(!isPaused);
			}
		}

		/// <summary>
		/// Updates the playback progress text.
		/// </summary>
		/// <param name="currentIndex">Zero-based current index.</param>
		/// <param name="total">Total items.</param>
		private void UpdatePlaybackProgress(int currentIndex, int total)
		{
			if (_playbackProgressText != null)
			{
				_playbackProgressText.text = total > 0
					? (currentIndex + 1) + " / " + total
					: string.Empty;
			}
		}

		/// <summary>
		/// Shows or hides the Play All button based on selection count.
		/// </summary>
		/// <param name="selectedCount">Number of selected journals.</param>
		private void UpdatePlayAllButton(int selectedCount)
		{
			if (_playAllButton != null)
			{
				_playAllButton.SetActive(selectedCount > 0);
			}
		}
	}
}
