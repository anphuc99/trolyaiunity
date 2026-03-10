using Core.Infrastructure.Views;
using Core.Infrastructure.Network;
using Core.Infrastructure.Authentication;
using Features.GamePlay.SubFeatures.Journal.Events;
using Share.Utils;
using Features.GamePlay.SubFeatures.Journal.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Journal.Model;
using Features.GamePlay.SubFeatures.Journal.Requests;
using Share.Components;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

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
		private JournalItemView _listItemTemplate;

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

		[Header("Selection")]
		[SerializeField] private Button _playAllButton;

		[Header("FSRS")]
		[SerializeField]
		private Button _fsrsButton;

		[SerializeField]
		private GameObject _fsrsContainer;
		[SerializeField]
		private Button _againButton;
		[SerializeField]
		private Button _hardButton;
		[SerializeField]
		private Button _goodButton;
		[SerializeField]
		private Button _easyButton;

		[Header("Buttons")]
		[SerializeField]
		private Button _autoPlayButton;
		[SerializeField]
		private Button _downloadAudioButton;

		private readonly List<JournalItemView> _spawnedListItems = new List<JournalItemView>();
		private readonly HashSet<int> _reloadingTtsMessageIndices = new HashSet<int>();
		private const float AutoPlayNextMessageDelaySeconds = 1f;
		private NetworkSettings _networkSettings;
		private string _normalizedServerBaseUrl;
		private List<MessageBubbleData> _currentChatMessages = new List<MessageBubbleData>();
		private bool _isChatAutoPlaying;
		private int _currentAutoPlayListIndex = -1;
		private bool _hasPausedAutoPlayClip;
		private bool _hasCapturedRunInBackground;
		private bool _previousRunInBackground;
		private bool _isBackgroundPlaybackActive;

		/// <summary>
		/// Tracks whether the view is in FSRS review mode.
		/// </summary>
		private bool _isFsrsMode;

		/// <summary>
		/// Cached due-journal list for advancing after a review.
		/// </summary>
		private List<JournalDueItemPayload> _dueJournals = new List<JournalDueItemPayload>();

		/// <summary>
		/// Index of the current due-journal being reviewed.
		/// </summary>
		private int _currentDueIndex;

		/// <summary>
		/// True while an audio download is in progress.
		/// </summary>
		private bool _isDownloadingAudio;

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

			if (_reloadingTtsMessageIndices.Count > 0)
			{
				var failedIndices = new List<int>(_reloadingTtsMessageIndices);
				for (var i = 0; i < failedIndices.Count; i++)
				{
					ClearReloadingState(failedIndices[i]);
				}

				if (_isChatAutoPlaying)
				{
					PlayNextAutoMessage();
				}
			}
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
				TryRestoreForegroundPlaybackMode();
				Debug.LogWarning("[JournalView] Missing audio URL for playback.", this);
				return;
			}

			EnsureAudioSource();
			EnableBackgroundPlaybackMode();
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
			EnsurePlayAllBinding();
			EnsureAutoPlayBinding();
			EnsureFsrsBindings();
			EnsureDownloadAudioBinding();
			_isFsrsMode = false;
			SetMode(false);
			SetFsrsContainerVisible(false);
			UpdateFsrsButtonText();
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
			StopChatAutoPlay(true);
			StopAllCoroutines();
			_reloadingTtsMessageIndices.Clear();
			_isFsrsMode = false;
			_isDownloadingAudio = false;
			_currentChatMessages.Clear();
			_dueJournals.Clear();
			_currentDueIndex = 0;
			if (_voiceAudioSource != null)
			{
				_voiceAudioSource.Stop();
			}

			TryRestoreForegroundPlaybackMode();

			gameObject.SetActive(false);
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			if (hasFocus || !ShouldKeepAudioAliveInBackground())
			{
				return;
			}

			AudioListener.pause = false;
		}

		private void OnApplicationPause(bool pauseStatus)
		{
			if (!pauseStatus || !ShouldKeepAudioAliveInBackground())
			{
				return;
			}

			AudioListener.pause = false;
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
				_listItemTemplate = _listContent.GetChild(0).GetComponent<JournalItemView>();
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
			if (!showDetail)
			{
				StopChatAutoPlay(true);
			}

			if (_listRoot != null)
			{
				_listRoot.SetActive(!showDetail);
			}

			if (_chatVariantRoot != null)
			{
				_chatVariantRoot.gameObject.SetActive(showDetail);
				_playAllButton.gameObject.SetActive(!showDetail);
			}

			if (_fsrsButton != null)
			{
				_fsrsButton.gameObject.SetActive(!showDetail);
			}

			if (_autoPlayButton != null)
			{
				_autoPlayButton.gameObject.SetActive(showDetail);
			}

			if (_downloadAudioButton != null)
			{
				_downloadAudioButton.gameObject.SetActive(showDetail);
			}

			// Show FSRS rating buttons only in FSRS detail mode
			SetFsrsContainerVisible(_isFsrsMode && showDetail);
			UpdateAutoPlayButtonText();
		}

		/// <summary>
		/// Renders journal list items with selection toggles for multi-select.
		/// </summary>
		/// <param name="journals">Journal summary items.</param>
		private void RenderJournalList(List<JournalListItemPayload> journals)
		{
			ClearSpawnedListItems();

			if (_listContent == null || _listItemTemplate == null)
			{
				return;
			}

			_listItemTemplate.gameObject.SetActive(false);
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
				instance.gameObject.SetActive(true);
				instance.Bind(journal.Id, BuildJournalListLabel(journal), false);
				_spawnedListItems.Add(instance);
				instance.Clicked += HandleItemClicked;
				instance.SelectionChanged += HandleItemSelectionChanged;
			}

			UpdatePlayAllButton(GetSelectedJournalCount());
		}

		/// <summary>
		/// Renders detail text for selected journal chat history.
		/// </summary>
		/// <param name="payload">Journal detail payload.</param>
		private void RenderJournalDetail(List<MessageBubbleData> messageBubbleData)
		{
			StopChatAutoPlay(true);
			_currentChatMessages = messageBubbleData ?? new List<MessageBubbleData>();
			_chatVariantRoot.SetChatHistory(messageBubbleData);
			UpdateAutoPlayButtonText();
		}

		private void HandleMessageSpeakerClicked(MessageBubbleData messageData)
		{
			if (messageData == null)
			{
				return;
			}

			if (_reloadingTtsMessageIndices.Contains(messageData.MessageIndex) || messageData.IsTtsReloading)
			{
				return;
			}

			RequestMessageAudio(messageData, false);
		}

		private void HandleMessageSpeakerLongPressed(MessageBubbleData messageData)
		{
			if (_chatVariantRoot == null || messageData == null)
			{
				return;
			}

			var messageIndex = messageData.MessageIndex;
			if (messageIndex < 0 || _reloadingTtsMessageIndices.Contains(messageIndex))
			{
				return;
			}

			RequestMessageAudio(messageData, true);
		}

		private IEnumerator PlayJournalAudioAsync(JournalPlayMessageAudioPayload payload)
		{
			var resolvedUrl = AudioUrlUtils.ResolveAudioUrl(payload.AudioUrl, GetNormalizedServerBaseUrl());
			if (string.IsNullOrWhiteSpace(resolvedUrl))
			{
				ClearReloadingState(payload.MessageIndex);
				TryRestoreForegroundPlaybackMode();
				yield break;
			}

			using var audioRequest = UnityWebRequestMultimedia.GetAudioClip(resolvedUrl, AudioUrlUtils.ResolveAudioType(resolvedUrl));
			yield return audioRequest.SendWebRequest();

			if (audioRequest.result != UnityWebRequest.Result.Success)
			{
				ClearReloadingState(payload.MessageIndex);
				TryRestoreForegroundPlaybackMode();
				Debug.LogWarning("[JournalView] Failed to download audio: " + audioRequest.error, this);
				yield break;
			}

			var clip = DownloadHandlerAudioClip.GetContent(audioRequest);
			if (clip == null)
			{
				ClearReloadingState(payload.MessageIndex);
				TryRestoreForegroundPlaybackMode();
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

			if (_isChatAutoPlaying)
			{
				yield return new WaitForSeconds(AutoPlayNextMessageDelaySeconds);

				if (!_isChatAutoPlaying)
				{
					TryRestoreForegroundPlaybackMode();
					yield break;
				}

				PlayNextAutoMessage();
				yield break;
			}

			TryRestoreForegroundPlaybackMode();
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

			_voiceAudioSource.ignoreListenerPause = true;
		}

		/// <summary>
		/// Returns the cached normalized server base URL (without trailing /api).
		/// </summary>
		private string GetNormalizedServerBaseUrl()
		{
			if (_normalizedServerBaseUrl == null)
			{
				if (_networkSettings == null)
				{
					_networkSettings = Resources.Load<NetworkSettings>("NetworkSettings");
				}

				_normalizedServerBaseUrl = AudioUrlUtils.NormalizeServerBaseUrl(
					_networkSettings != null ? _networkSettings.BaseUrl : null) ?? string.Empty;
			}

			return string.IsNullOrWhiteSpace(_normalizedServerBaseUrl) ? null : _normalizedServerBaseUrl;
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

			if (_listContent == null)
			{
				return;
			}

			for (var i = _listContent.childCount - 1; i >= 0; i--)
			{
				var child = _listContent.GetChild(i);
				if (child == null)
				{
					continue;
				}

				if (_listItemTemplate != null && child == _listItemTemplate.transform)
				{
					continue;
				}

				Destroy(child.gameObject);
			}
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
			StopChatAutoPlay(true);
			_currentChatMessages.Clear();
			_currentAutoPlayListIndex = -1;
			UpdateAutoPlayButtonText();

			if (_isFsrsMode)
			{
				// Return to due-journals list instead of normal list
				SetMode(false);
				RenderDueJournalList(_dueJournals);
				return;
			}

			SetMode(false);
		}

		/// <summary>
		/// Handles journal item click from the item view.
		/// </summary>
		/// <param name="item">Item view that fired the click.</param>
		private void HandleItemClicked(JournalItemView item)
		{
			if (item == null || item.JournalId <= 0)
			{
				return;
			}

			// Track the current due index if in FSRS mode
			if (_isFsrsMode)
			{
				for (var i = 0; i < _dueJournals.Count; i++)
				{
					if (_dueJournals[i].Id == item.JournalId)
					{
						_currentDueIndex = i;
						break;
					}
				}
			}

			SendRequest(JournalRequests.LoadJournalDetail, new JournalDetailRequestPayload
			{
				JournalId = item.JournalId
			});
		}

		/// <summary>
		/// Handles selection change from the item view.
		/// </summary>
		/// <param name="item">Item view that fired the change.</param>
		/// <param name="isSelected">Current selection state.</param>
		private void HandleItemSelectionChanged(JournalItemView item, bool isSelected)
		{
			UpdatePlayAllButton(GetSelectedJournalCount());
		}

		// ==================================================================
		// Play All button helper
		// ==================================================================

		/// <summary>
		/// Wires the Play All button to send the StartPlayback request.
		/// </summary>
		private void EnsurePlayAllBinding()
		{
			if (_playAllButton != null)
			{
				_playAllButton.onClick.RemoveAllListeners();
				_playAllButton.onClick.AddListener(() =>
					SendRequest(JournalRequests.StartPlayback, new JournalStartPlaybackRequestPayload
					{
						SelectedIds = GetSelectedJournalIds()
					}));
			}
		}

		/// <summary>
		/// Wires the Auto Play button to toggle sequential message playback.
		/// </summary>
		private void EnsureAutoPlayBinding()
		{
			if (_autoPlayButton == null)
			{
				return;
			}

			_autoPlayButton.onClick.RemoveAllListeners();
			_autoPlayButton.onClick.AddListener(HandleAutoPlayButtonClicked);
			UpdateAutoPlayButtonText();
		}

		/// <summary>
		/// Toggles auto play for currently displayed chat history.
		/// </summary>
		private void HandleAutoPlayButtonClicked()
		{
			if (_isChatAutoPlaying)
			{
				StopChatAutoPlay();
				return;
			}

			if (_currentChatMessages == null || _currentChatMessages.Count == 0)
			{
				return;
			}

			_isChatAutoPlaying = true;
			UpdateAutoPlayButtonText();

			if (_hasPausedAutoPlayClip && _voiceAudioSource != null && _voiceAudioSource.clip != null)
			{
				_hasPausedAutoPlayClip = false;
				_voiceAudioSource.UnPause();
				return;
			}

			PlayNextAutoMessage(_currentAutoPlayListIndex >= 0);
		}

		/// <summary>
		/// Stops auto play state and restores button label.
		/// </summary>
		/// <param name="resetSequencePosition">
		/// True to clear the current message pointer (use when leaving chat or loading a new chat).
		/// False to keep current pointer so pressing play can continue from the paused message.
		/// </param>
		private void StopChatAutoPlay(bool resetSequencePosition = false)
		{
			_isChatAutoPlaying = false;
			if (_voiceAudioSource != null)
			{
				if (resetSequencePosition)
				{
					_voiceAudioSource.Stop();
					_hasPausedAutoPlayClip = false;
				}
				else if (_voiceAudioSource.isPlaying)
				{
					_voiceAudioSource.Pause();
					_hasPausedAutoPlayClip = true;
				}
				else
				{
					_hasPausedAutoPlayClip = false;
				}
			}
			else
			{
				_hasPausedAutoPlayClip = false;
			}

			if (resetSequencePosition)
			{
				_currentAutoPlayListIndex = -1;
			}

			if (_reloadingTtsMessageIndices.Count > 0)
			{
				var pendingIndices = new List<int>(_reloadingTtsMessageIndices);
				for (var i = 0; i < pendingIndices.Count; i++)
				{
					ClearReloadingState(pendingIndices[i]);
				}
			}

			UpdateAutoPlayButtonText();
			TryRestoreForegroundPlaybackMode();
		}

		/// <summary>
		/// Enables app/background audio mode while journal audio is playing.
		/// </summary>
		private void EnableBackgroundPlaybackMode()
		{
			if (!_hasCapturedRunInBackground)
			{
				_previousRunInBackground = Application.runInBackground;
				_hasCapturedRunInBackground = true;
			}

			Application.runInBackground = true;
			_isBackgroundPlaybackActive = true;

			if (_voiceAudioSource != null)
			{
				_voiceAudioSource.ignoreListenerPause = true;
			}

			AudioListener.pause = false;
		}

		/// <summary>
		/// Restores original foreground-only mode when no journal audio is active.
		/// </summary>
		private void TryRestoreForegroundPlaybackMode()
		{
			if (!_isBackgroundPlaybackActive)
			{
				return;
			}

			if (_isChatAutoPlaying)
			{
				return;
			}

			if (_voiceAudioSource != null && _voiceAudioSource.isPlaying)
			{
				return;
			}

			if (_hasCapturedRunInBackground)
			{
				Application.runInBackground = _previousRunInBackground;
			}

			_isBackgroundPlaybackActive = false;
		}

		/// <summary>
		/// Indicates whether the journal view should keep audio alive while app is unfocused.
		/// </summary>
		/// <returns>True when background audio mode is currently active.</returns>
		private bool ShouldKeepAudioAliveInBackground()
		{
			if (!_isBackgroundPlaybackActive)
			{
				return false;
			}

			if (_isChatAutoPlaying)
			{
				return true;
			}

			return _voiceAudioSource != null && _voiceAudioSource.isPlaying;
		}

		/// <summary>
		/// Requests audio for the next playable message and loops to the start at the end.
		/// </summary>
		/// <param name="allowCurrentIndex">
		/// True to first attempt replaying current index (resume case), false to move to next index.
		/// </param>
		private void PlayNextAutoMessage(bool allowCurrentIndex = false)
		{
			if (!_isChatAutoPlaying || _currentChatMessages == null || _currentChatMessages.Count == 0)
			{
				return;
			}

			var count = _currentChatMessages.Count;
			var startOffset = allowCurrentIndex ? 0 : 1;
			for (var offset = startOffset; offset <= count; offset++)
			{
				var listIndex = (_currentAutoPlayListIndex + offset + count) % count;
				var messageData = _currentChatMessages[listIndex];
				if (!CanAutoPlayMessage(messageData))
				{
					continue;
				}

				_currentAutoPlayListIndex = listIndex;
				if (_chatVariantRoot != null)
				{
					_chatVariantRoot.ScrollToMessage(messageData.MessageIndex);
				}

				RequestMessageAudio(messageData, false);
				return;
			}

			// No playable messages, keep loop disabled.
			StopChatAutoPlay(true);
		}

		/// <summary>
		/// Checks whether a message can be used in auto-play sequence.
		/// </summary>
		/// <param name="messageData">Candidate message data.</param>
		/// <returns>True when message has playable text and valid index.</returns>
		private static bool CanAutoPlayMessage(MessageBubbleData messageData)
		{
			if (messageData == null || messageData.MessageIndex < 0)
			{
				return false;
			}

			var text = string.IsNullOrWhiteSpace(messageData.OriginalMessage)
				? messageData.Message
				: messageData.OriginalMessage;

			return !string.IsNullOrWhiteSpace(text);
		}

		/// <summary>
		/// Sends TTS audio request for one message bubble.
		/// </summary>
		/// <param name="messageData">Target message data.</param>
		/// <param name="forceReload">Whether to force server regeneration.</param>
		private void RequestMessageAudio(MessageBubbleData messageData, bool forceReload)
		{
			if (messageData == null)
			{
				return;
			}

			var text = string.IsNullOrWhiteSpace(messageData.OriginalMessage) ? messageData.Message : messageData.OriginalMessage;
			if (string.IsNullOrWhiteSpace(text))
			{
				return;
			}

			_reloadingTtsMessageIndices.Add(messageData.MessageIndex);
			if (_chatVariantRoot != null)
			{
				_chatVariantRoot.SetMessageTtsReloading(messageData.MessageIndex, true);
			}

			SendRequest(JournalRequests.PlayMessageAudio, new JournalPlayMessageAudioRequestPayload
			{
				MessageId = messageData.MessageId,
				MessageIndex = messageData.MessageIndex,
				CharacterName = messageData.Type == MessageBubbleType.User ? "User" : messageData.SenderName,
				Text = text,
				Tone = messageData.Tone,
				ForceReload = forceReload
			});
		}

		/// <summary>
		/// Updates Auto Play button label based on current state.
		/// </summary>
		private void UpdateAutoPlayButtonText()
		{
			if (_autoPlayButton == null)
			{
				return;
			}

			var label = _autoPlayButton.GetComponentInChildren<TMP_Text>();
			if (label != null)
			{
				label.text = _isChatAutoPlaying ? "Dừng tự phát" : "Tự phát";
			}
		}
		// ==================================================================
		/// <summary>
		/// Builds a list of selected journal ids from list items.
		/// </summary>
		/// <returns>Selected journal ids.</returns>
		private List<int> GetSelectedJournalIds()
		{
			var selectedIds = new List<int>();
			for (var i = 0; i < _spawnedListItems.Count; i++)
			{
				var item = _spawnedListItems[i];
				if (item != null && item.IsSelected && item.JournalId > 0)
				{
					selectedIds.Add(item.JournalId);
				}
			}

			return selectedIds;
		}

		/// <summary>
		/// Counts the selected journals in the current list.
		/// </summary>
		/// <returns>Selection count.</returns>
		private int GetSelectedJournalCount()
		{
			var count = 0;
			for (var i = 0; i < _spawnedListItems.Count; i++)
			{
				var item = _spawnedListItems[i];
				if (item != null && item.IsSelected)
				{
					count++;
				}
			}

			return count;
		}
		// ==================================================================
		/// <summary>
		/// Shows or hides the Play All button based on selection count.
		/// </summary>
		/// <param name="selectedCount">Number of selected journals.</param>
		private void UpdatePlayAllButton(int selectedCount)
		{
			if (_playAllButton != null)
			{
				_playAllButton.interactable = selectedCount > 0;
			}
		}

		// ==================================================================
		// FSRS Review helpers
		// ==================================================================

		/// <summary>
		/// Wires the FSRS button and rating buttons.
		/// </summary>
		private void EnsureFsrsBindings()
		{
			if (_fsrsButton != null)
			{
				_fsrsButton.onClick.RemoveAllListeners();
				_fsrsButton.onClick.AddListener(HandleFsrsButtonClicked);
			}

			if (_againButton != null)
			{
				_againButton.onClick.RemoveAllListeners();
				_againButton.onClick.AddListener(() => SubmitFsrsRating(1));
			}

			if (_hardButton != null)
			{
				_hardButton.onClick.RemoveAllListeners();
				_hardButton.onClick.AddListener(() => SubmitFsrsRating(2));
			}

			if (_goodButton != null)
			{
				_goodButton.onClick.RemoveAllListeners();
				_goodButton.onClick.AddListener(() => SubmitFsrsRating(3));
			}

			if (_easyButton != null)
			{
				_easyButton.onClick.RemoveAllListeners();
				_easyButton.onClick.AddListener(() => SubmitFsrsRating(4));
			}
		}

		/// <summary>
		/// Handles the FSRS button click — toggles between normal and FSRS modes.
		/// In normal mode: loads due journals for review.
		/// In FSRS mode: exits review mode and reloads all journals.
		/// </summary>
		private void HandleFsrsButtonClicked()
		{
			if (_isFsrsMode)
			{
				// Exit FSRS mode and reload all journals
				_isFsrsMode = false;
				_dueJournals.Clear();
				_currentDueIndex = 0;
				SetFsrsContainerVisible(false);
				UpdateFsrsButtonText();
				LoadJournals();
			}
			else
			{
				// Enter FSRS mode and load due journals
				_isFsrsMode = true;
				UpdateFsrsButtonText();
				SendRequest(JournalRequests.LoadDueJournals, null);
			}
		}

		/// <summary>
		/// Submits the current FSRS rating for the currently viewed journal.
		/// </summary>
		/// <param name="rating">1=Again, 2=Hard, 3=Good, 4=Easy.</param>
		private void SubmitFsrsRating(int rating)
		{
			var journalId = JournalState.SelectedJournalId;
			if (!journalId.HasValue || journalId.Value <= 0)
			{
				Debug.LogWarning("[JournalView] No journal selected for FSRS rating.", this);
				return;
			}

			SendRequest(JournalRequests.SubmitJournalReview, new JournalSubmitReviewRequestPayload
			{
				JournalId = journalId.Value,
				Rating = rating
			});
		}

		/// <summary>
		/// Shows or hides the FSRS rating container.
		/// </summary>
		/// <param name="visible">True to show.</param>
		private void SetFsrsContainerVisible(bool visible)
		{
			if (_fsrsContainer != null)
			{
				_fsrsContainer.SetActive(visible);
			}
		}

		/// <summary>
		/// Updates the FSRS button label based on current mode.
		/// Normal mode: "Review lại" / FSRS mode: "Quay lại".
		/// </summary>
		private void UpdateFsrsButtonText()
		{
			if (_fsrsButton == null)
			{
				return;
			}

			var label = _fsrsButton.GetComponentInChildren<TMP_Text>();
			if (label != null)
			{
				label.text = _isFsrsMode ? "Quay lại" : "Review lại";
			}
		}

		/// <summary>
		/// Handles due-journals-loaded event from controller.
		/// </summary>
		/// <param name="payload">Due journals response.</param>
		[OnEvent(JournalEvents.DueJournalsLoaded)]
		private void OnDueJournalsLoaded(object payload)
		{
			if (payload is not JournalDueListResponsePayload response)
			{
				return;
			}

			EnsureBindings();
			_isFsrsMode = true;
			_dueJournals = response.Journals ?? new List<JournalDueItemPayload>();
			_currentDueIndex = 0;
			RenderDueJournalList(_dueJournals);
			SetMode(false);
		}

		/// <summary>
		/// Handles journal review submitted event.
		/// Advances to the next due journal or returns to list.
		/// </summary>
		/// <param name="payload">Review response payload.</param>
		[OnEvent(JournalEvents.ReviewSubmitted)]
		private void OnReviewSubmitted(object payload)
		{
			if (!_isFsrsMode)
			{
				return;
			}

			// Remove the reviewed journal from the due list
			var reviewedId = JournalState.SelectedJournalId;
			if (reviewedId.HasValue)
			{
				_dueJournals.RemoveAll(j => j.Id == reviewedId.Value);
			}

			// Advance to the next due journal or return to list
			if (_dueJournals.Count > 0)
			{
				if (_currentDueIndex >= _dueJournals.Count)
				{
					_currentDueIndex = 0;
				}

				var next = _dueJournals[_currentDueIndex];
				SendRequest(JournalRequests.LoadJournalDetail, new JournalDetailRequestPayload
				{
					JournalId = next.Id
				});
			}
			else
			{
				// All reviewed — exit FSRS mode and go back to normal list
				_isFsrsMode = false;
				SetFsrsContainerVisible(false);
				UpdateFsrsButtonText();
				LoadJournals();
			}
		}

		/// <summary>
		/// Renders the due-journal list using the same item template.
		/// </summary>
		/// <param name="dueJournals">Due journal items.</param>
		private void RenderDueJournalList(List<JournalDueItemPayload> dueJournals)
		{
			ClearSpawnedListItems();

			if (_listContent == null || _listItemTemplate == null)
			{
				return;
			}

			_listItemTemplate.gameObject.SetActive(false);
			if (dueJournals == null || dueJournals.Count == 0)
			{
				UpdatePlayAllButton(0);
				return;
			}

			for (var i = 0; i < dueJournals.Count; i++)
			{
				var journal = dueJournals[i];
				if (journal == null)
				{
					continue;
				}

				var instance = Instantiate(_listItemTemplate, _listContent);
				instance.name = "JournalItem-" + journal.Id;
				instance.gameObject.SetActive(true);
				var label = BuildDueJournalLabel(journal);
				instance.Bind(journal.Id, label, false);
				_spawnedListItems.Add(instance);
				instance.Clicked += HandleItemClicked;
				instance.SelectionChanged += HandleItemSelectionChanged;
			}

			UpdatePlayAllButton(GetSelectedJournalCount());
		}

		/// <summary>
		/// Builds the display label for a due-journal item.
		/// Shows "[New]" or "[Due]" prefix.
		/// </summary>
		/// <param name="journal">Due journal item.</param>
		/// <returns>Formatted label text.</returns>
		private static string BuildDueJournalLabel(JournalDueItemPayload journal)
		{
			var prefix = journal.Review == null ? "[Mới]" : "[Ôn tập]";
			var summary = string.IsNullOrWhiteSpace(journal.Summary) ? "(Không có tóm tắt)" : journal.Summary.Trim();
			if (DateTime.TryParse(journal.CreatedAt, out var createdAt))
			{
				return prefix + " " + createdAt.ToString("dd/MM/yyyy HH:mm") + " - " + summary;
			}

			return prefix + " " + summary;
		}

		// ==================================================================
		// Download Audio
		// ==================================================================

		/// <summary>
		/// Wires the download audio button to send the DownloadAudio request.
		/// </summary>
		private void EnsureDownloadAudioBinding()
		{
			if (_downloadAudioButton == null)
			{
				return;
			}

			_downloadAudioButton.onClick.RemoveAllListeners();
			_downloadAudioButton.onClick.AddListener(HandleDownloadAudioClicked);
		}

		/// <summary>
		/// Handles the download audio button press.
		/// </summary>
		private void HandleDownloadAudioClicked()
		{
			if (_isDownloadingAudio)
			{
				return;
			}

			var journalId = JournalState.SelectedJournalId;
			if (!journalId.HasValue || journalId.Value <= 0)
			{
				Debug.LogWarning("[JournalView] No journal selected for audio download.", this);
				return;
			}

			SendRequest(JournalRequests.DownloadAudio, new JournalDetailRequestPayload
			{
				JournalId = journalId.Value
			});
		}

		/// <summary>
		/// Receives resolved download URL from controller and starts the download coroutine.
		/// </summary>
		/// <param name="payload">Audio download payload with URL.</param>
		[OnEvent(JournalEvents.AudioDownloadCompleted)]
		private void OnAudioDownloadCompleted(object payload)
		{
			if (payload is not JournalAudioDownloadPayload downloadPayload)
			{
				return;
			}

			if (string.IsNullOrWhiteSpace(downloadPayload.DownloadUrl))
			{
				Debug.LogError("[JournalView] Download URL is empty.", this);
				return;
			}

			StartCoroutine(DownloadAndSaveAudioAsync(downloadPayload));
		}

		/// <summary>
		/// Downloads the combined journal MP3 from the server and saves to persistent storage.
		/// </summary>
		/// <param name="payload">Download payload with URL and journal id.</param>
		/// <returns>Coroutine enumerator.</returns>
		private IEnumerator DownloadAndSaveAudioAsync(JournalAudioDownloadPayload payload)
		{
			_isDownloadingAudio = true;
			UpdateDownloadButtonText(true);

			using var request = UnityWebRequest.Get(payload.DownloadUrl);
			var token = AuthTokenModel.AccessToken;
			if (!string.IsNullOrWhiteSpace(token))
			{
				request.SetRequestHeader("Authorization", "Bearer " + token);
			}

			yield return request.SendWebRequest();

			if (request.result != UnityWebRequest.Result.Success)
			{
				Debug.LogError("[JournalView] Audio download failed: " + request.error, this);
				_isDownloadingAudio = false;
				UpdateDownloadButtonText(false);
				yield break;
			}

			var audioData = request.downloadHandler.data;
			if (audioData == null || audioData.Length == 0)
			{
				Debug.LogError("[JournalView] Downloaded audio is empty.", this);
				_isDownloadingAudio = false;
				UpdateDownloadButtonText(false);
				yield break;
			}

			var fileName = "journal_" + payload.JournalId + "_audio.mp3";
			var savePath = Path.Combine(Application.persistentDataPath, fileName);

			try
			{
				File.WriteAllBytes(savePath, audioData);
				Debug.Log("[JournalView] Audio saved to: " + savePath, this);
			}
			catch (Exception ex)
			{
				Debug.LogError("[JournalView] Failed to save audio: " + ex.Message, this);
			}

			_isDownloadingAudio = false;
			UpdateDownloadButtonText(false);
		}

		/// <summary>
		/// Updates the download button label during and after download.
		/// </summary>
		/// <param name="isDownloading">True while download is in progress.</param>
		private void UpdateDownloadButtonText(bool isDownloading)
		{
			if (_downloadAudioButton == null)
			{
				return;
			}

			var label = _downloadAudioButton.GetComponentInChildren<TMP_Text>();
			if (label != null)
			{
				label.text = isDownloading ? "Đang tải..." : "Tải audio";
			}

			_downloadAudioButton.interactable = !isDownloading;
		}
	}
}