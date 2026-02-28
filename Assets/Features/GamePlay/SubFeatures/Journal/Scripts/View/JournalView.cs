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

		private readonly List<JournalItemView> _spawnedListItems = new List<JournalItemView>();
		private readonly HashSet<int> _reloadingTtsMessageIndices = new HashSet<int>();
		private NetworkSettings _networkSettings;

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
			EnsurePlayAllBinding();
			SetMode(false);
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
			StopAllCoroutines();
			_reloadingTtsMessageIndices.Clear();
			if (_voiceAudioSource != null)
			{
				_voiceAudioSource.Stop();
			}

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
			if (_listRoot != null)
			{
				_listRoot.SetActive(!showDetail);
			}

			if (_chatVariantRoot != null)
			{
				_chatVariantRoot.gameObject.SetActive(showDetail);
				_playAllButton.gameObject.SetActive(!showDetail);
			}
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
	}
}