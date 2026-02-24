using Core.Infrastructure.Views;
using Core.Infrastructure.Network;
using Core.Infrastructure.Authentication;
using Features.GamePlay.SubFeatures.Chat.Events;
using Features.GamePlay.SubFeatures.Chat.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Chat.Model;
using Features.GamePlay.SubFeatures.Chat.Requests;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace Features.GamePlay.SubFeatures.Chat.View
{
	/// <summary>
	/// View for Chat.
	/// </summary>
	public sealed class ChatView : BaseView
	{
		private const string DefaultUserDisplayName = "You";
		private const string DefaultCharacterDisplayName = "Mimi";
		private const string DefaultTtsTone = "neutral";

		[SerializeField]
		private TMP_InputField _inputField;

		[SerializeField]
		private VirtualizedChatMessageContainer _messageContainer;

		[SerializeField]
		private string _sessionId = "default";

		[SerializeField]
		private string _modelOverride;

		[SerializeField]
		private int _storyId;

		[SerializeField]
		private AudioSource _characterVoiceAudioSource;

		private readonly Queue<ChatAssistantTurnPayload> _pendingCharacterTurns = new Queue<ChatAssistantTurnPayload>();
		private readonly HashSet<int> _reloadingTtsMessageIndices = new HashSet<int>();
		private bool _isProcessingCharacterTurns;
		private NetworkSettings _networkSettings;

		/// <summary>
		/// Sends current input field text to chat server.
		/// </summary>
		public void SendInputMessage()
		{
			if (_inputField == null)
			{
				return;
			}

			SendChatMessage(_inputField.text);
		}

		protected override void OnDisabled()
		{
			UnbindInputFieldEvents();
		}

		/// <summary>
		/// Sends one user message to server and appends it locally.
		/// </summary>
		/// <param name="message">User message content.</param>
		public void SendChatMessage(string message)
		{
			if (_messageContainer == null)
			{
				return;
			}

			var trimmed = message?.Trim();
			if (string.IsNullOrWhiteSpace(trimmed))
			{
				return;
			}

			_messageContainer.AddNewMessage(new MessageBubbleData
			{
				MessageId = Guid.NewGuid().ToString("N"),
				Type = MessageBubbleType.User,
				SenderName = DefaultUserDisplayName,
				Message = trimmed,
				Avatar = null,
			});

			SendRequest(ChatRequests.SendMessage, new ChatSendRequestPayload
			{
				Message = trimmed,
				SessionId = string.IsNullOrWhiteSpace(_sessionId) ? null : _sessionId,
				Model = string.IsNullOrWhiteSpace(_modelOverride) ? null : _modelOverride,
				StoryId = _storyId > 0 ? _storyId : null,
			});

			if (_inputField != null)
			{
				_inputField.text = string.Empty;
				_inputField.ActivateInputField();
			}
		}

		/// <summary>
		/// Requests current chat history from server.
		/// </summary>
		public void RefreshHistory()
		{
			SendRequest(ChatRequests.LoadHistory, new ChatHistoryRequestPayload
			{
				SessionId = string.IsNullOrWhiteSpace(_sessionId) ? null : _sessionId,
			});
		}

		/// <summary>
		/// Called when view is enabled and scope is active.
		/// </summary>
		protected override void OnEnabled()
		{
			EnsureDependencies();
			RefreshHistory();
			BindInputFieldEvents();
		}

		/// <summary>
		/// Shows this subfeature view when its controller is installed.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(ChatEvents.Installed)]
		private void OnInstalled(object payload)
		{
			gameObject.SetActive(true);
			EnsureDependencies();
			RefreshHistory();
		}

		/// <summary>
		/// Hides this subfeature view when its controller is uninstalled.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(ChatEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			StopAllCoroutines();
			_pendingCharacterTurns.Clear();
			_isProcessingCharacterTurns = false;
			if (_messageContainer != null)
			{
				_messageContainer.OnMessageSpeakerClicked = null;
				_messageContainer.OnMessageSpeakerLongPressed = null;
				_messageContainer.OnMessageTranslateClicked = null;
			}
			_reloadingTtsMessageIndices.Clear();
			if (_characterVoiceAudioSource != null)
			{
				_characterVoiceAudioSource.Stop();
			}
			gameObject.SetActive(false);
		}

		/// <summary>
		/// Binds server history into virtualized chat message container.
		/// </summary>
		/// <param name="payload">History response payload.</param>
		[OnEvent(ChatEvents.HistoryLoaded)]
		private void OnHistoryLoaded(object payload)
		{
			if (_messageContainer == null)
			{
				return;
			}

			var response = payload as ChatHistoryResponsePayload;
			if (response == null || response.Messages == null)
			{
				_messageContainer.SetMessages(new List<MessageBubbleData>());
				return;
			}

			var mapped = new List<MessageBubbleData>(response.Messages.Count);
			for (var i = 0; i < response.Messages.Count; i++)
			{
				var item = response.Messages[i];
				if (item == null)
				{
					continue;
				}

				var isUser = string.Equals(item.Role, "user", StringComparison.OrdinalIgnoreCase);
				if (isUser)
				{
					mapped.Add(new MessageBubbleData
					{
						MessageId = Guid.NewGuid().ToString("N"),
						Type = MessageBubbleType.User,
						SenderName = DefaultUserDisplayName,
						Message = item.Content ?? string.Empty,
						OriginalMessage = item.Content ?? string.Empty,
						Avatar = null,
					});
					continue;
				}

				var turns = ParseAssistantTurns(item.Content);
				if (turns.Count == 0)
				{
					mapped.Add(new MessageBubbleData
					{
						MessageId = Guid.NewGuid().ToString("N"),
						Type = MessageBubbleType.Character,
						SenderName = DefaultCharacterDisplayName,
						Message = item.Content ?? string.Empty,
						OriginalMessage = item.Content ?? string.Empty,
						Tone = DefaultTtsTone,
						Avatar = SendRequest<Sprite>(ChatRequests.GetCharacterAvatar, DefaultCharacterDisplayName),
					});
					continue;
				}

				for (var turnIndex = 0; turnIndex < turns.Count; turnIndex++)
				{
					var turn = turns[turnIndex];
					if (turn == null)
					{
						continue;
					}

					var characterName = string.IsNullOrWhiteSpace(turn.CharacterName) ? DefaultCharacterDisplayName : turn.CharacterName.Trim();
					var text = string.IsNullOrWhiteSpace(turn.Text) ? string.Empty : turn.Text;
					mapped.Add(new MessageBubbleData
					{
						MessageId = string.IsNullOrWhiteSpace(turn.MessageId) ? Guid.NewGuid().ToString("N") : turn.MessageId,
						Type = MessageBubbleType.Character,
						SenderName = characterName,
						Message = text,
						OriginalMessage = text,
						Translation = turn.Translation,
						Tone = string.IsNullOrWhiteSpace(turn.Tone) ? DefaultTtsTone : turn.Tone.Trim(),
						Avatar = SendRequest<Sprite>(ChatRequests.GetCharacterAvatar, characterName),
					});
				}
			}

			_messageContainer.SetMessages(mapped);
		}

		/// <summary>
		/// Handles assistant message received event.
		/// </summary>
		/// <param name="payload">Assistant payload.</param>
		[OnEvent(ChatEvents.MessageReceived)]
		private void OnMessageReceived(object payload)
		{
			if (_messageContainer == null)
			{
				return;
			}

			var response = payload as ChatAssistantMessagePayload;
			if (response == null || string.IsNullOrWhiteSpace(response.Reply))
			{
				return;
			}

			var turns = response.Turns != null && response.Turns.Count > 0 ? response.Turns : ParseAssistantTurns(response.Reply);
			if (turns.Count == 0)
			{
				_messageContainer.AddNewMessage(new MessageBubbleData
				{
					MessageId = Guid.NewGuid().ToString("N"),
					Type = MessageBubbleType.Character,
					SenderName = DefaultCharacterDisplayName,
					Message = response.Reply,
					OriginalMessage = response.Reply,
					Tone = DefaultTtsTone,
					Avatar = SendRequest<Sprite>(ChatRequests.GetCharacterAvatar, DefaultCharacterDisplayName),
				});
				return;
			}

			for (var i = 0; i < turns.Count; i++)
			{
				var turn = turns[i];
				if (turn == null)
				{
					continue;
				}

				_pendingCharacterTurns.Enqueue(turn);
			}

			if (!_isProcessingCharacterTurns)
			{
				StartCoroutine(ProcessCharacterTurnsSequentially());
			}
		}

		private IEnumerator ProcessCharacterTurnsSequentially()
		{
			_isProcessingCharacterTurns = true;

			while (_pendingCharacterTurns.Count > 0)
			{
				if (_messageContainer == null)
				{
					break;
				}

				var turn = _pendingCharacterTurns.Dequeue();
				if (turn == null)
				{
					continue;
				}

				var characterName = string.IsNullOrWhiteSpace(turn.CharacterName) ? DefaultCharacterDisplayName : turn.CharacterName.Trim();
				var messageText = turn.Text ?? string.Empty;
				if (string.IsNullOrWhiteSpace(messageText))
				{
					continue;
				}

				var tone = string.IsNullOrWhiteSpace(turn.Tone) ? DefaultTtsTone : turn.Tone.Trim();

				AudioClip clip = null;
				yield return StartCoroutine(RequestCharacterTtsClip(messageText, tone, characterName, loadedClip =>
				{
					clip = loadedClip;
				}));

				_messageContainer.AddNewMessage(new MessageBubbleData
				{
					MessageId = string.IsNullOrWhiteSpace(turn.MessageId) ? Guid.NewGuid().ToString("N") : turn.MessageId,
					Type = MessageBubbleType.Character,
					SenderName = characterName,
					Message = messageText,
					OriginalMessage = messageText,
					Translation = turn.Translation,
					Tone = tone,
					Avatar = SendRequest<Sprite>(ChatRequests.GetCharacterAvatar, characterName),
				});

				if (clip != null)
				{
					yield return StartCoroutine(PlayCharacterVoiceAsync(clip));
				}
			}

			_isProcessingCharacterTurns = false;
		}

		private IEnumerator RequestCharacterTtsClip(string text, string tone, string characterName, Action<AudioClip> onCompleted)
		{
			yield return RequestCharacterTtsClip(text, tone, characterName, false, onCompleted);
		}

		private IEnumerator RequestCharacterTtsClip(string text, string tone, string characterName, bool forceReload, Action<AudioClip> onCompleted)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				onCompleted?.Invoke(null);
				yield break;
			}

			var requestUrl = BuildTextToSpeechRequestUrl(text, tone, characterName, forceReload);
			if (string.IsNullOrWhiteSpace(requestUrl))
			{
				onCompleted?.Invoke(null);
				yield break;
			}

			using var request = UnityWebRequest.Get(requestUrl);
			var accessToken = AuthTokenModel.AccessToken;
			if (!string.IsNullOrWhiteSpace(accessToken))
			{
				request.SetRequestHeader("Authorization", "Bearer " + accessToken);
			}
			yield return request.SendWebRequest();

			if (request.result != UnityWebRequest.Result.Success)
			{
				Debug.LogWarning("[ChatView] TTS request failed: " + request.error, this);
				onCompleted?.Invoke(null);
				yield break;
			}

			ChatTextToSpeechResponsePayload ttsResponse;
			try
			{
				ttsResponse = JsonConvert.DeserializeObject<ChatTextToSpeechResponsePayload>(request.downloadHandler.text);
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[ChatView] Failed to parse TTS response: " + exception.Message, this);
				onCompleted?.Invoke(null);
				yield break;
			}

			var audioUrl = ResolveAudioUrl(ttsResponse?.Url);
			if (string.IsNullOrWhiteSpace(audioUrl))
			{
				onCompleted?.Invoke(null);
				yield break;
			}

			using var audioRequest = UnityWebRequestMultimedia.GetAudioClip(audioUrl, ResolveAudioType(audioUrl));
			yield return audioRequest.SendWebRequest();

			if (audioRequest.result != UnityWebRequest.Result.Success)
			{
				Debug.LogWarning("[ChatView] Failed to download TTS audio: " + audioRequest.error, this);
				onCompleted?.Invoke(null);
				yield break;
			}

			var clip = DownloadHandlerAudioClip.GetContent(audioRequest);
			onCompleted?.Invoke(clip);
		}

		private IEnumerator PlayCharacterVoiceAsync(AudioClip clip)
		{
			if (clip == null)
			{
				yield break;
			}

			EnsureDependencies();
			if (_characterVoiceAudioSource == null)
			{
				yield break;
			}

			_characterVoiceAudioSource.Stop();
			_characterVoiceAudioSource.clip = clip;
			_characterVoiceAudioSource.Play();
			while (_characterVoiceAudioSource.isPlaying)
			{
				yield return null;
			}

		}

		private void EnsureDependencies()
		{
			if (_networkSettings == null)
			{
				_networkSettings = Resources.Load<NetworkSettings>("NetworkSettings");
			}

			if (_characterVoiceAudioSource == null)
			{
				_characterVoiceAudioSource = GetComponent<AudioSource>();
				if (_characterVoiceAudioSource == null)
				{
					_characterVoiceAudioSource = gameObject.AddComponent<AudioSource>();
				}
			}

			if (_messageContainer != null)
			{
				_messageContainer.OnMessageSpeakerClicked = HandleMessageSpeakerClicked;
				_messageContainer.OnMessageSpeakerLongPressed = HandleMessageSpeakerLongPressed;
				_messageContainer.OnMessageTranslateClicked = HandleMessageTranslateClicked;
			}

			BindInputFieldEvents();
		}

		private void BindInputFieldEvents()
		{
			if (_inputField == null)
			{
				return;
			}

			_inputField.onSubmit.RemoveListener(HandleInputSubmitted);
			_inputField.onSubmit.AddListener(HandleInputSubmitted);
			_inputField.onEndEdit.RemoveListener(HandleInputEndEdit);
			_inputField.onEndEdit.AddListener(HandleInputEndEdit);
		}

		private void UnbindInputFieldEvents()
		{
			if (_inputField == null)
			{
				return;
			}

			_inputField.onSubmit.RemoveListener(HandleInputSubmitted);
			_inputField.onEndEdit.RemoveListener(HandleInputEndEdit);
		}

		private void HandleInputSubmitted(string value)
		{
			SendInputMessage();
		}

		private void HandleInputEndEdit(string value)
		{
			if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter))
			{
				return;
			}

			SendInputMessage();
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

			if (string.IsNullOrWhiteSpace(messageData.Message))
			{
				return;
			}

			SendRequest(ChatRequests.PlayMessageAudio, new ChatPlayMessageAudioRequestPayload
			{
				MessageId = messageData.MessageId,
				CharacterName = messageData.SenderName,
				Text = string.IsNullOrWhiteSpace(messageData.OriginalMessage) ? messageData.Message : messageData.OriginalMessage,
				Tone = string.IsNullOrWhiteSpace(messageData.Tone) ? DefaultTtsTone : messageData.Tone,
			});
		}

		private void HandleMessageSpeakerLongPressed(MessageBubbleData messageData)
		{
			if (_messageContainer == null || messageData == null || messageData.Type != MessageBubbleType.Character)
			{
				return;
			}

			if (messageData.MessageIndex < 0 || _reloadingTtsMessageIndices.Contains(messageData.MessageIndex))
			{
				return;
			}

			StartCoroutine(ForceReloadMessageTts(messageData));
		}

		private IEnumerator ForceReloadMessageTts(MessageBubbleData messageData)
		{
			if (_messageContainer == null || messageData == null || messageData.MessageIndex < 0)
			{
				yield break;
			}

			var messageIndex = messageData.MessageIndex;
			_reloadingTtsMessageIndices.Add(messageIndex);
			_messageContainer.SetMessageTtsReloading(messageIndex, true);

			var baseText = string.IsNullOrWhiteSpace(messageData.OriginalMessage) ? messageData.Message : messageData.OriginalMessage;
			if (string.IsNullOrWhiteSpace(baseText))
			{
				_messageContainer.SetMessageTtsReloading(messageIndex, false);
				_reloadingTtsMessageIndices.Remove(messageIndex);
				yield break;
			}

			var tone = string.IsNullOrWhiteSpace(messageData.Tone) ? DefaultTtsTone : messageData.Tone.Trim();
			var characterName = string.IsNullOrWhiteSpace(messageData.SenderName) ? DefaultCharacterDisplayName : messageData.SenderName.Trim();

			AudioClip clip = null;
			yield return StartCoroutine(RequestCharacterTtsClip(baseText, tone, characterName, true, loadedClip =>
			{
				clip = loadedClip;
			}));

			if (clip != null)
			{
				yield return StartCoroutine(PlayCharacterVoiceAsync(clip));
			}

			_messageContainer.SetMessageTtsReloading(messageIndex, false);
			_reloadingTtsMessageIndices.Remove(messageIndex);
		}

		private void HandleMessageTranslateClicked(MessageBubbleData messageData)
		{
			if (_messageContainer == null || messageData == null)
			{
				return;
			}

			if (messageData.MessageIndex < 0 && string.IsNullOrWhiteSpace(messageData.Translation))
			{
				return;
			}

			_messageContainer.ToggleMessageTranslation(messageData);
		}

		/// <summary>
		/// Handles controller-approved message-audio playback.
		/// </summary>
		/// <param name="payload">Playback payload.</param>
		[OnEvent(ChatEvents.MessageAudioPlayRequested)]
		private void OnMessageAudioPlayRequested(object payload)
		{
			var playback = payload as ChatPlayMessageAudioPayload;
			if (playback == null || string.IsNullOrWhiteSpace(playback.Text))
			{
				return;
			}

			StartCoroutine(PlaySingleMessageAudio(playback));
		}

		private IEnumerator PlaySingleMessageAudio(ChatPlayMessageAudioPayload playback)
		{
			var tone = string.IsNullOrWhiteSpace(playback.Tone) ? DefaultTtsTone : playback.Tone.Trim();
			AudioClip clip = null;
			yield return StartCoroutine(RequestCharacterTtsClip(playback.Text, tone, playback.CharacterName, loadedClip =>
			{
				clip = loadedClip;
			}));

			if (clip == null)
			{
				yield break;
			}

			yield return StartCoroutine(PlayCharacterVoiceAsync(clip));
		}

		private string BuildTextToSpeechRequestUrl(string text, string tone, string characterName, bool forceReload = false)
		{
			var baseUrl = NormalizeServerBaseUrl(GetServerBaseUrl());
			if (string.IsNullOrWhiteSpace(baseUrl))
			{
				return null;
			}

			var endpoint = baseUrl + NetworkEndpoints.TextToSpeech;
			var queryParts = new List<string>
			{
				"text=" + UnityWebRequest.EscapeURL(text),
				"tone=" + UnityWebRequest.EscapeURL(string.IsNullOrWhiteSpace(tone) ? DefaultTtsTone : tone),
				"characterName=" + UnityWebRequest.EscapeURL(string.IsNullOrWhiteSpace(characterName) ? DefaultCharacterDisplayName : characterName),
			};

			if (forceReload)
			{
				queryParts.Add("force=true");
			}

			return endpoint + "?" + string.Join("&", queryParts);
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

		private static List<ChatAssistantTurnPayload> ParseAssistantTurns(string content)
		{
			if (string.IsNullOrWhiteSpace(content))
			{
				return new List<ChatAssistantTurnPayload>();
			}

			try
			{
				var list = JsonConvert.DeserializeObject<List<ChatAssistantTurnPayload>>(content);
				if (list != null && list.Count > 0)
				{
					return list;
				}

				var single = JsonConvert.DeserializeObject<ChatAssistantTurnPayload>(content);
				if (single != null)
				{
					return new List<ChatAssistantTurnPayload> { single };
				}
			}
			catch
			{
			}

			return new List<ChatAssistantTurnPayload>();
		}

		/// <summary>
		/// Logs chat request failures.
		/// </summary>
		/// <param name="payload">Error payload.</param>
		[OnEvent(ChatEvents.RequestFailed)]
		private void OnRequestFailed(object payload)
		{
			var error = payload as ChatErrorPayload;
			Debug.LogWarning("[ChatView] Chat request failed: " + (error?.Message ?? "Unknown error"), this);
		}
	}
}
