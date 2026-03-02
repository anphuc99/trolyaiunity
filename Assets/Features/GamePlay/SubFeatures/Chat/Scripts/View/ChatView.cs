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
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using Share.Components;
using UnityEngine.UI;

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
		private const int RecordingFrequencyHz = 16000;
		private const int MaxRecordingSeconds = 60;
		private const string DefaultSpeechLanguage = "ko";

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

		[SerializeField]
		private ChatSelectCharacterPopupView _selectCharacterPopupView;

		[SerializeField]
		private ChatContextPopupView _contextPopupView;

		[SerializeField]
		private TMP_InputField _intputChat;

		[SerializeField]
		private Button _recordButton;

		[SerializeField]
		private Button _sendButton;

		private readonly Queue<ChatAssistantTurnPayload> _pendingCharacterTurns = new Queue<ChatAssistantTurnPayload>();
		private readonly HashSet<int> _reloadingTtsMessageIndices = new HashSet<int>();
		private bool _isProcessingCharacterTurns;
		private bool _isCharacterResponding;
		private bool _isPopupInitialized;
		private bool _isContextPopupInitialized;
		private bool _isRecordingVoice;
		private bool _isTranscribingVoice;
		private string _recordingDeviceName;
		private AudioClip _recordingAudioClip;
		private Image _recordButtonImage;
		private Color _recordButtonIdleColor = Color.white;
		private NetworkSettings _networkSettings;

		/// <summary>
		/// Sends current input field text to chat server.
		/// </summary>
		public void SendInputMessage()
		{
			if (_isCharacterResponding)
			{
				return;
			}

			if (_inputField == null)
			{
				return;
			}

			SendChatMessage(_inputField.text);
		}

		protected override void OnDisabled()
		{
			StopRecordingIfNeeded();
			UnbindInputFieldEvents();
			UnbindRecordButtonEvents();
		}

		/// <summary>
		/// Sends one user message to server and appends it locally.
		/// </summary>
		/// <param name="message">User message content.</param>
		public void SendChatMessage(string message)
		{
			if (_isCharacterResponding)
			{
				return;
			}

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
			ScrollMessagesToBottom();

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
			StopRecordingIfNeeded();
			_pendingCharacterTurns.Clear();
			_isProcessingCharacterTurns = false;
			_isTranscribingVoice = false;
			UpdateRecordButtonVisualState();
			SetCharacterRespondingState(false);
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
			if (_selectCharacterPopupView != null)
			{
				_selectCharacterPopupView.HideImmediate();
			}
			if (_contextPopupView != null)
			{
				_contextPopupView.HideImmediate();
			}
			gameObject.SetActive(false);
		}

		/// <summary>
		/// Displays add-character popup with character list from controller.
		/// </summary>
		/// <param name="payload">Character list payload.</param>
		[OnEvent(ChatEvents.CharactersLoaded)]
		private void OnCharactersLoaded(object payload)
		{
			EnsureDependencies();
			if (_selectCharacterPopupView == null)
			{
				return;
			}

			if (payload is List<ChatSelectableCharacterPayload> characters)
			{
				_selectCharacterPopupView.Show(characters);
				return;
			}

			_selectCharacterPopupView.Show(new List<ChatSelectableCharacterPayload>());
		}

		/// <summary>
		/// Opens context popup when requested from controller.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(ChatEvents.ContextInputRequested)]
		private void OnContextInputRequested(object payload)
		{
			EnsureDependencies();
			if (_contextPopupView == null)
			{
				return;
			}

			_contextPopupView.Show();
		}

		/// <summary>
		/// Clears chat UI and navigates back to the Home tab after ending the conversation.
		/// </summary>
		/// <param name="payload">Optional end-conversation response payload.</param>
		[OnEvent(ChatEvents.ConversationEnded)]
		private void OnConversationEnded(object payload)
		{
			ClearConversationState();
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
				ScrollMessagesToBottom();
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
			ScrollMessagesToBottom();
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
				ScrollMessagesToBottom();
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

			SetCharacterRespondingState(true);

			if (!_isProcessingCharacterTurns)
			{
				StartCoroutine(ProcessCharacterTurnsSequentially());
			}
		}

		private IEnumerator ProcessCharacterTurnsSequentially()
		{
			_isProcessingCharacterTurns = true;
			SetCharacterRespondingState(true);

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
				ScrollMessagesToBottom();

				if (clip != null)
				{
					yield return StartCoroutine(PlayCharacterVoiceAsync(clip));
				}
			}

			_isProcessingCharacterTurns = false;
			SetCharacterRespondingState(false);
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

			if (_selectCharacterPopupView == null)
			{
				var popupTransform = FindChildByName(transform, "PopupSelectCharacter");
				if (popupTransform != null)
				{
					_selectCharacterPopupView = popupTransform.GetComponent<ChatSelectCharacterPopupView>();
					if (_selectCharacterPopupView == null)
					{
						_selectCharacterPopupView = popupTransform.gameObject.AddComponent<ChatSelectCharacterPopupView>();
					}
				}
			}

			if (_contextPopupView == null)
			{
				var contextPopupTransform = FindChildByName(transform, "PopupContext");
				if (contextPopupTransform != null)
				{
					_contextPopupView = contextPopupTransform.GetComponent<ChatContextPopupView>();
					if (_contextPopupView == null)
					{
						_contextPopupView = contextPopupTransform.gameObject.AddComponent<ChatContextPopupView>();
					}
				}
			}

			if (_selectCharacterPopupView != null && !_isPopupInitialized)
			{
				_selectCharacterPopupView.HideImmediate();
				_isPopupInitialized = true;
			}

			if (_selectCharacterPopupView != null)
			{
				_selectCharacterPopupView.OnCharacterToggleChanged = HandleCharacterToggleChanged;
			}

			if (_contextPopupView != null && !_isContextPopupInitialized)
			{
				_contextPopupView.HideImmediate();
				_isContextPopupInitialized = true;
			}

			if (_contextPopupView != null)
			{
				_contextPopupView.OnSaveContextClicked = HandleSaveContextClicked;
				_contextPopupView.OnSaveAndSendContextClicked = HandleSaveAndSendContextClicked;
			}

			if (_messageContainer != null)
			{
				_messageContainer.OnMessageSpeakerClicked = HandleMessageSpeakerClicked;
				_messageContainer.OnMessageSpeakerLongPressed = HandleMessageSpeakerLongPressed;
				_messageContainer.OnMessageTranslateClicked = HandleMessageTranslateClicked;
			}

			if (_recordButton != null)
			{
				_recordButtonImage = _recordButton.targetGraphic as Image;
			}

			BindRecordButtonEvents();
			UpdateRecordButtonVisualState();

			SetChatInputInteractable(!_isCharacterResponding);

			BindInputFieldEvents();
		}

		private void SetCharacterRespondingState(bool isResponding)
		{
			_isCharacterResponding = isResponding;
			SetChatInputInteractable(!isResponding);
		}

		private void ScrollMessagesToBottom()
		{
			if (_messageContainer == null)
			{
				return;
			}

			_messageContainer.ScrollToBottom();
		}

		private void SetChatInputInteractable(bool isInteractable)
		{
			if (_inputField != null)
			{
				_inputField.interactable = isInteractable;
				if (!isInteractable)
				{
					_inputField.DeactivateInputField();
				}
			}

			if (_intputChat != null)
			{
				_intputChat.interactable = isInteractable;
				if (!isInteractable)
				{
					_intputChat.DeactivateInputField();
				}
			}

			if (_sendButton != null)
			{
				_sendButton.interactable = isInteractable;
			}

			if (_recordButton != null)
			{
				_recordButton.interactable = isInteractable && !_isTranscribingVoice;
			}
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

		private void BindRecordButtonEvents()
		{
			if (_recordButton == null)
			{
				return;
			}

			_recordButton.onClick.RemoveListener(HandleRecordButtonClicked);
			_recordButton.onClick.AddListener(HandleRecordButtonClicked);
		}

		private void UnbindRecordButtonEvents()
		{
			if (_recordButton == null)
			{
				return;
			}

			_recordButton.onClick.RemoveListener(HandleRecordButtonClicked);
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

		private void HandleRecordButtonClicked()
		{
			if (_isCharacterResponding || _isTranscribingVoice)
			{
				return;
			}

			if (_isRecordingVoice)
			{
				StopAndTranscribeRecording();
				return;
			}

			StartVoiceRecording();
		}

		private void StartVoiceRecording()
		{
			if (Microphone.devices == null || Microphone.devices.Length == 0)
			{
				Debug.LogWarning("[ChatView] No microphone device found.", this);
				return;
			}

			_recordingDeviceName = Microphone.devices[0];
			_recordingAudioClip = Microphone.Start(_recordingDeviceName, false, MaxRecordingSeconds, RecordingFrequencyHz);
			if (_recordingAudioClip == null)
			{
				Debug.LogWarning("[ChatView] Failed to start voice recording.", this);
				return;
			}

			_isRecordingVoice = true;
			UpdateRecordButtonVisualState();
		}

		private void StopAndTranscribeRecording()
		{
			if (!_isRecordingVoice)
			{
				return;
			}

			var deviceName = _recordingDeviceName;
			var recordedClip = _recordingAudioClip;
			var sampleCount = 0;
			if (!string.IsNullOrWhiteSpace(deviceName))
			{
				sampleCount = Microphone.GetPosition(deviceName);
				Microphone.End(deviceName);
			}

			_isRecordingVoice = false;
			_recordingDeviceName = null;
			_recordingAudioClip = null;
			UpdateRecordButtonVisualState();

			if (recordedClip == null || sampleCount <= 0)
			{
				return;
			}

			var trimmedClip = TrimAudioClip(recordedClip, sampleCount);
			if (trimmedClip == null)
			{
				return;
			}

			StartCoroutine(TranscribeRecordedAudio(trimmedClip));
		}

		private void StopRecordingIfNeeded()
		{
			if (!_isRecordingVoice)
			{
				return;
			}

			if (!string.IsNullOrWhiteSpace(_recordingDeviceName))
			{
				Microphone.End(_recordingDeviceName);
			}

			_isRecordingVoice = false;
			_recordingDeviceName = null;
			_recordingAudioClip = null;
			UpdateRecordButtonVisualState();
		}

		private IEnumerator TranscribeRecordedAudio(AudioClip clip)
		{
			if (clip == null)
			{
				yield break;
			}

			var endpoint = BuildSpeechToTextRequestUrl();
			if (string.IsNullOrWhiteSpace(endpoint))
			{
				yield break;
			}

			var wavBytes = ConvertClipToWav(clip);
			if (wavBytes == null || wavBytes.Length == 0)
			{
				yield break;
			}

			var requestPayload = new ChatSpeechToTextRequestPayload
			{
				Audio = "data:audio/wav;base64," + Convert.ToBase64String(wavBytes),
				Language = DefaultSpeechLanguage,
			};

			var requestJson = JsonConvert.SerializeObject(requestPayload);
			var requestBytes = Encoding.UTF8.GetBytes(requestJson);
			using var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST)
			{
				uploadHandler = new UploadHandlerRaw(requestBytes),
				downloadHandler = new DownloadHandlerBuffer(),
			};
			request.SetRequestHeader("Content-Type", "application/json");

			var accessToken = AuthTokenModel.AccessToken;
			if (!string.IsNullOrWhiteSpace(accessToken))
			{
				request.SetRequestHeader("Authorization", "Bearer " + accessToken);
			}

			_isTranscribingVoice = true;
			SetChatInputInteractable(!_isCharacterResponding);
			UpdateRecordButtonVisualState();

			yield return request.SendWebRequest();

			_isTranscribingVoice = false;
			SetChatInputInteractable(!_isCharacterResponding);
			UpdateRecordButtonVisualState();

			if (request.result != UnityWebRequest.Result.Success)
			{
				Debug.LogWarning("[ChatView] Speech-to-text request failed: " + request.error, this);
				yield break;
			}

			ChatSpeechToTextResponsePayload response;
			try
			{
				response = JsonConvert.DeserializeObject<ChatSpeechToTextResponsePayload>(request.downloadHandler.text);
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[ChatView] Failed to parse speech-to-text response: " + exception.Message, this);
				yield break;
			}

			var transcript = response?.Transcript?.Trim();
			if (string.IsNullOrWhiteSpace(transcript))
			{
				yield break;
			}

			if (_inputField != null)
			{
				_inputField.text = transcript;
				_inputField.ActivateInputField();
			}

			if (_intputChat != null && _intputChat != _inputField)
			{
				_intputChat.text = transcript;
				_intputChat.ActivateInputField();
			}
		}

		private void UpdateRecordButtonVisualState()
		{
			if (_recordButtonImage == null)
			{
				return;
			}

			_recordButtonImage.color = _isRecordingVoice ? Color.red : _recordButtonIdleColor;
		}

		private string BuildSpeechToTextRequestUrl()
		{
			var baseUrl = NormalizeServerBaseUrl(GetServerBaseUrl());
			if (string.IsNullOrWhiteSpace(baseUrl))
			{
				return null;
			}

			return baseUrl + NetworkEndpoints.ChatTranscribe;
		}

		private static AudioClip TrimAudioClip(AudioClip sourceClip, int sampleCount)
		{
			if (sourceClip == null || sampleCount <= 0)
			{
				return null;
			}

			sampleCount = Mathf.Clamp(sampleCount, 1, sourceClip.samples);
			var channelCount = sourceClip.channels;
			var sourceData = new float[sourceClip.samples * channelCount];
			sourceClip.GetData(sourceData, 0);

			var trimmedData = new float[sampleCount * channelCount];
			Array.Copy(sourceData, trimmedData, trimmedData.Length);

			var trimmedClip = AudioClip.Create("chat-recorded", sampleCount, channelCount, sourceClip.frequency, false);
			trimmedClip.SetData(trimmedData, 0);
			return trimmedClip;
		}

		private static byte[] ConvertClipToWav(AudioClip clip)
		{
			if (clip == null)
			{
				return null;
			}

			var sampleCount = clip.samples;
			var channelCount = clip.channels;
			var frequency = clip.frequency;
			var samples = new float[sampleCount * channelCount];
			clip.GetData(samples, 0);

			var pcmBytes = new byte[samples.Length * 2];
			for (var index = 0; index < samples.Length; index++)
			{
				var value = Mathf.Clamp(samples[index], -1f, 1f);
				short pcmValue = (short)Mathf.RoundToInt(value * short.MaxValue);
				pcmBytes[index * 2] = (byte)(pcmValue & 0xff);
				pcmBytes[index * 2 + 1] = (byte)((pcmValue >> 8) & 0xff);
			}

			var headerSize = 44;
			var wavBytes = new byte[headerSize + pcmBytes.Length];

			Encoding.ASCII.GetBytes("RIFF").CopyTo(wavBytes, 0);
			BitConverter.GetBytes(wavBytes.Length - 8).CopyTo(wavBytes, 4);
			Encoding.ASCII.GetBytes("WAVE").CopyTo(wavBytes, 8);
			Encoding.ASCII.GetBytes("fmt ").CopyTo(wavBytes, 12);
			BitConverter.GetBytes(16).CopyTo(wavBytes, 16);
			BitConverter.GetBytes((short)1).CopyTo(wavBytes, 20);
			BitConverter.GetBytes((short)channelCount).CopyTo(wavBytes, 22);
			BitConverter.GetBytes(frequency).CopyTo(wavBytes, 24);
			BitConverter.GetBytes(frequency * channelCount * 2).CopyTo(wavBytes, 28);
			BitConverter.GetBytes((short)(channelCount * 2)).CopyTo(wavBytes, 32);
			BitConverter.GetBytes((short)16).CopyTo(wavBytes, 34);
			Encoding.ASCII.GetBytes("data").CopyTo(wavBytes, 36);
			BitConverter.GetBytes(pcmBytes.Length).CopyTo(wavBytes, 40);
			pcmBytes.CopyTo(wavBytes, headerSize);

			return wavBytes;
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

		private void HandleCharacterToggleChanged(ChatSelectableCharacterPayload payload, bool isOn)
		{
			if (payload == null || string.IsNullOrWhiteSpace(payload.Name))
			{
				return;
			}

			SendRequest(ChatRequests.SetCharacterActive, new ChatSetCharacterActiveRequestPayload
			{
				SessionId = string.IsNullOrWhiteSpace(_sessionId) ? null : _sessionId,
				CharacterName = payload.Name,
				IsActive = isOn,
			});
		}

		private void HandleSaveContextClicked(string context)
		{
			SendRequest(ChatRequests.SaveContext, new ChatSaveContextRequestPayload
			{
				SessionId = string.IsNullOrWhiteSpace(_sessionId) ? null : _sessionId,
				Context = context,
			});
		}

		private void HandleSaveAndSendContextClicked(string context)
		{
			HandleSaveContextClicked(context);

			var messageToSend = ResolveCurrentInputMessage();
			if (string.IsNullOrWhiteSpace(messageToSend))
			{
				messageToSend = context;
			}

			if (string.IsNullOrWhiteSpace(messageToSend))
			{
				return;
			}

			SendChatMessage(messageToSend);
		}

		private string ResolveCurrentInputMessage()
		{
			var primaryText = _inputField != null ? _inputField.text : null;
			if (!string.IsNullOrWhiteSpace(primaryText))
			{
				return primaryText;
			}

			var secondaryText = _intputChat != null ? _intputChat.text : null;
			if (!string.IsNullOrWhiteSpace(secondaryText))
			{
				return secondaryText;
			}

			return string.Empty;
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

		private static Transform FindChildByName(Transform root, string targetName)
		{
			if (root == null || string.IsNullOrWhiteSpace(targetName))
			{
				return null;
			}

			for (var i = 0; i < root.childCount; i++)
			{
				var child = root.GetChild(i);
				if (child == null)
				{
					continue;
				}

				if (string.Equals(child.name, targetName, StringComparison.Ordinal))
				{
					return child;
				}

				var nested = FindChildByName(child, targetName);
				if (nested != null)
				{
					return nested;
				}
			}

			return null;
		}

		private void ClearConversationState()
		{
			StopAllCoroutines();
			StopRecordingIfNeeded();
			_pendingCharacterTurns.Clear();
			_isProcessingCharacterTurns = false;
			SetCharacterRespondingState(false);
			_reloadingTtsMessageIndices.Clear();

			if (_characterVoiceAudioSource != null)
			{
				_characterVoiceAudioSource.Stop();
			}

			if (_messageContainer != null)
			{
				_messageContainer.SetMessages(new List<MessageBubbleData>());
				ScrollMessagesToBottom();
			}

			if (_inputField != null)
			{
				_inputField.text = string.Empty;
				_inputField.DeactivateInputField();
			}
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
