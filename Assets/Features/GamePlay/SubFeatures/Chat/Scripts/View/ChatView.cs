using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Chat.Events;
using Features.GamePlay.SubFeatures.Chat.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Chat.Model;
using Features.GamePlay.SubFeatures.Chat.Requests;
using Share.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
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
		private const string DefaultSpeechLanguage = "zh";

		[SerializeField]
		private TMP_InputField _inputField;

		[SerializeField]
		private VirtualizedChatMessageContainer _messageContainer;

		[SerializeField]
		private string _sessionId = "default";

		[SerializeField]
		private string _modelOverride;

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

		[SerializeField]
		private SharedVocabularyPopupView _vocabPopupView;

		[SerializeField]
		private TextMeshProUGUI _countVocabText;

		/// <summary>
		/// Regex to match **word** vocabulary markup in assistant text.
		/// </summary>
		private static readonly Regex VocabMarkupRegex = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);

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

		/// <summary>
		/// Rich text marker shown in the input field when a voice recording is pending.
		/// </summary>
		private const string AudioRecordingRichText = "<i>Bản ghi âm</i>";

		/// <summary>
		/// Base64-encoded audio data URL of the pending voice recording.
		/// Set when the user finishes recording; cleared when sent or cancelled.
		/// </summary>
		private string _pendingAudioBase64;

		/// <summary>
		/// The message id assigned to the user bubble for a pending audio message.
		/// Used to update the bubble text once transcription arrives from server.
		/// </summary>
		private string _pendingAudioMessageId;

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
			UnbindAudioInputGuard();
		}

		/// <summary>
		/// Sends one user message to server and appends it locally.
		/// When a pending voice recording exists, the audio data is sent alongside
		/// the message so Gemini can process the audio directly.
		/// </summary>
		/// <param name="message">User message content.</param>
		public void SendChatMessage(string message)
		{
			if (_isCharacterResponding)
			{
				return;
			}

			if (!HasAnySceneCharacter())
			{
				Debug.LogWarning("[ChatView] Chat is blocked because scene has no characters.", this);
				return;
			}

			if (_messageContainer == null)
			{
				return;
			}

			var trimmed = message?.Trim();
			var hasAudio = !string.IsNullOrEmpty(_pendingAudioBase64);
			var userTextForTracking = trimmed ?? string.Empty;

			if (string.IsNullOrWhiteSpace(trimmed) && !hasAudio)
			{
				return;
			}

			SetCharacterRespondingState(true);

			var messageId = Guid.NewGuid().ToString("N");
			var displayText = hasAudio ? AudioRecordingRichText : trimmed;

			_messageContainer.AddNewMessage(new MessageBubbleData
			{
				MessageId = messageId,
				Type = MessageBubbleType.User,
				SenderName = DefaultUserDisplayName,
				Message = displayText,
				Avatar = null,
			});
			ScrollMessagesToBottom();

			var payload = new ChatSendRequestPayload
			{
				Message = hasAudio ? null : trimmed,
				SessionId = string.IsNullOrWhiteSpace(_sessionId) ? null : _sessionId,
				Model = string.IsNullOrWhiteSpace(_modelOverride) ? null : _modelOverride,
			};

			if (hasAudio)
			{
				payload.Audio = _pendingAudioBase64;
				payload.AudioMessageId = messageId;
				_pendingAudioMessageId = messageId;
			}

			_pendingAudioBase64 = null;
			UnbindAudioInputGuard();

			SendRequest(ChatRequests.SendMessage, payload);

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
		/// Requests learned vocabulary count from server.
		/// </summary>
		public void RefreshVocabularyLearnedCount()
		{
			SendRequest(ChatRequests.LoadVocabularyLearnedCount);
		}

		/// <summary>
		/// Called when view is enabled and scope is active.
		/// </summary>
		protected override void OnEnabled()
		{
			EnsureDependencies();
			RefreshHistory();
			RefreshVocabularyLearnedCount();
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
			SendRequest(ChatRequests.LoadDeveloperState);
			RefreshHistory();
			RefreshVocabularyLearnedCount();
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
			UnbindAudioInputGuard();
			_pendingAudioBase64 = null;
			_pendingAudioMessageId = null;
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
				_messageContainer.OnVocabWordClicked = null;
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
		/// Handles request from controller to end conversation.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(ChatEvents.EndConversationRequested)]
		private void OnEndConversationRequested(object payload)
		{
			SendRequest(ChatRequests.EndConversation);
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

				var turns = item.Turns != null && item.Turns.Count > 0 ? item.Turns : new List<ChatAssistantTurnPayload>();
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
						Message = ConvertVocabMarkupToRichText(text),
						OriginalMessage = StripVocabMarkup(text),
						RawVocabText = text,
						Translation = turn.Translation,
						Pinyin = turn.Pinyin,
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

			var turns = response.Turns != null && response.Turns.Count > 0 ? response.Turns : new List<ChatAssistantTurnPayload>();
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

				// Wait until this turn finishes preload so text is displayed together with ready audio.
				while (turn.AudioClip == null && !turn.IsAudioPreloadCompleted)
				{
					yield return null;
				}

				_messageContainer.AddNewMessage(new MessageBubbleData
				{
					MessageId = string.IsNullOrWhiteSpace(turn.MessageId) ? Guid.NewGuid().ToString("N") : turn.MessageId,
					Type = MessageBubbleType.Character,
					SenderName = characterName,
					Message = ConvertVocabMarkupToRichText(messageText),
					OriginalMessage = StripVocabMarkup(messageText),
					RawVocabText = messageText,
					Translation = turn.Translation,
					Pinyin = turn.Pinyin,
					Tone = tone,
					Avatar = SendRequest<Sprite>(ChatRequests.GetCharacterAvatar, characterName),
				});
				ScrollMessagesToBottom();

				if (turn.AudioClip != null)
				{
					yield return StartCoroutine(PlayCharacterVoiceAsync(turn.AudioClip));
				}
			}

			_isProcessingCharacterTurns = false;
			SetCharacterRespondingState(false);
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
				var popupTransform = TransformUtils.FindChildByName(transform, "PopupSelectCharacter");
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
				var contextPopupTransform = TransformUtils.FindChildByName(transform, "PopupContext");
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
				_messageContainer.OnVocabWordClicked = HandleVocabWordClicked;
			}

			if (_vocabPopupView != null)
			{
				_vocabPopupView.SetRatingButtonsVisible(true);
				_vocabPopupView.SetReviewCallback(HandleVocabReviewRequested);
				_vocabPopupView.SetClosedCallback(HandleVocabPopupClosed);
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

		/// <summary>
		/// Checks whether scene currently contains at least one chat character.
		/// </summary>
		/// <returns>True when at least one character exists in scene.</returns>
		private bool HasAnySceneCharacter()
		{
			return SendRequest<bool>(ChatRequests.HasAnySceneCharacter);
		}

		private void BindInputFieldEvents()
		{
			if (_inputField == null)
			{
				return;
			}

			_inputField.onSubmit.RemoveListener(HandleInputSubmitted);
			_inputField.onSubmit.AddListener(HandleInputSubmitted);
		}

		private void UnbindInputFieldEvents()
		{
			if (_inputField == null)
			{
				return;
			}

			_inputField.onSubmit.RemoveListener(HandleInputSubmitted);
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

		private void HandleRecordButtonClicked()
		{
			if (_isCharacterResponding || _isTranscribingVoice)
			{
				return;
			}

			if (!_isRecordingVoice && !HasAnySceneCharacter())
			{
				Debug.LogWarning("[ChatView] Voice chat is blocked because scene has no characters.", this);
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

		/// <summary>
		/// Stops voice recording, encodes the audio as base64, and places the
		/// rich text recording marker into the input field. The audio data is stored
		/// in _pendingAudioBase64 and will be sent alongside the next chat message.
		/// </summary>
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

			var trimmedClip = AudioConvertUtils.TrimAudioClip(recordedClip, sampleCount);
			if (trimmedClip == null)
			{
				return;
			}

			var wavBytes = AudioConvertUtils.ConvertClipToWav(trimmedClip);
			if (wavBytes == null || wavBytes.Length == 0)
			{
				return;
			}

			// Store audio data and show recording marker in input field
			_pendingAudioBase64 = "data:audio/wav;base64," + Convert.ToBase64String(wavBytes);

			if (_inputField != null)
			{
				_inputField.richText = true;
				_inputField.text = AudioRecordingRichText;
			}

			BindAudioInputGuard();
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
			_pendingAudioBase64 = null;
			UpdateRecordButtonVisualState();
		}

		/// <summary>
		/// Binds the input field value-changed listener that enforces the audio
		/// recording rich text rules: any deletion clears the field entirely,
		/// and any addition resets the field back to the recording marker.
		/// </summary>
		private void BindAudioInputGuard()
		{
			if (_inputField == null)
			{
				return;
			}

			_inputField.onValueChanged.RemoveListener(HandleAudioInputGuard);
			_inputField.onValueChanged.AddListener(HandleAudioInputGuard);
		}

		/// <summary>
		/// Unbinds the audio input guard listener.
		/// </summary>
		private void UnbindAudioInputGuard()
		{
			if (_inputField == null)
			{
				return;
			}

			_inputField.onValueChanged.RemoveListener(HandleAudioInputGuard);
		}

		/// <summary>
		/// Enforces the recording marker in the input field.
		/// If the marker lost any character (deletion), clears the entire input and discards the recording.
		/// If new characters were added, resets back to the marker only.
		/// </summary>
		/// <param name="newValue">Current input field text value.</param>
		private void HandleAudioInputGuard(string newValue)
		{
			if (string.IsNullOrEmpty(_pendingAudioBase64))
			{
				UnbindAudioInputGuard();
				return;
			}

			if (string.IsNullOrEmpty(newValue))
			{
				// User deleted everything
				ClearPendingAudioRecording();
				return;
			}

			// Check if the recording marker is still intact
			if (newValue.Contains(AudioRecordingRichText))
			{
				// Marker intact but extra characters were added — reset to marker only
				if (!string.Equals(newValue, AudioRecordingRichText, StringComparison.Ordinal))
				{
					_inputField.onValueChanged.RemoveListener(HandleAudioInputGuard);
					_inputField.text = AudioRecordingRichText;
					_inputField.onValueChanged.AddListener(HandleAudioInputGuard);
				}
				return;
			}

			// Marker is broken (characters were deleted) — clear everything
			ClearPendingAudioRecording();
		}

		/// <summary>
		/// Discards the pending audio recording and clears the input field.
		/// </summary>
		private void ClearPendingAudioRecording()
		{
			_pendingAudioBase64 = null;
			_pendingAudioMessageId = null;
			UnbindAudioInputGuard();

			if (_inputField != null)
			{
				_inputField.onValueChanged.RemoveListener(HandleAudioInputGuard);
				_inputField.text = string.Empty;
			}
		}

		/// <summary>
		/// Handles transcription completion from the Controller (legacy OpenAI STT path).
		/// </summary>
		/// <param name="payload">Transcript string, or null on failure.</param>
		[OnEvent(ChatEvents.TranscriptionCompleted)]
		private void OnTranscriptionCompleted(object payload)
		{
			_isTranscribingVoice = false;
			SetChatInputInteractable(!_isCharacterResponding);
			UpdateRecordButtonVisualState();

			var transcript = payload as string;
			if (string.IsNullOrWhiteSpace(transcript))
			{
				return;
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

		/// <summary>
		/// Handles audio transcription from Gemini after a voice message was sent.
		/// Updates the user bubble text with the actual transcribed content.
		/// </summary>
		/// <param name="payload">ChatAudioTranscribedPayload with transcription and message id.</param>
		[OnEvent(ChatEvents.AudioRecordingTranscribed)]
		private void OnAudioRecordingTranscribed(object payload)
		{
			var transcribed = payload as ChatAudioTranscribedPayload;
			if (transcribed == null || string.IsNullOrWhiteSpace(transcribed.Transcribe))
			{
				return;
			}

			if (_messageContainer == null)
			{
				return;
			}

			if (!string.IsNullOrWhiteSpace(transcribed.UserMessageId))
			{
				_messageContainer.UpdateMessageText(transcribed.UserMessageId, transcribed.Transcribe);
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

		private void HandleMessageSpeakerClicked(MessageBubbleData messageData)
		{
			if (messageData == null)
			{
				return;
			}

			if (_reloadingTtsMessageIndices.Contains(messageData.MessageIndex) || messageData.IsTtsReloading || messageData.IsTtsPlaying)
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
				CharacterName = messageData.Type == MessageBubbleType.User ? "User" : messageData.SenderName,
				Text = string.IsNullOrWhiteSpace(messageData.OriginalMessage) ? messageData.Message : messageData.OriginalMessage,
				Tone = string.IsNullOrWhiteSpace(messageData.Tone) ? DefaultTtsTone : messageData.Tone,
			});
		}

		private void HandleMessageSpeakerLongPressed(MessageBubbleData messageData)
		{
			if (_messageContainer == null || messageData == null)
			{
				return;
			}

			if (messageData.MessageIndex < 0 || _reloadingTtsMessageIndices.Contains(messageData.MessageIndex))
			{
				return;
			}

			var baseText = string.IsNullOrWhiteSpace(messageData.OriginalMessage) ? messageData.Message : messageData.OriginalMessage;
			if (string.IsNullOrWhiteSpace(baseText))
			{
				return;
			}

			_reloadingTtsMessageIndices.Add(messageData.MessageIndex);
			_messageContainer.SetMessageTtsReloading(messageData.MessageIndex, true);

			var tone = string.IsNullOrWhiteSpace(messageData.Tone) ? DefaultTtsTone : messageData.Tone.Trim();
			var characterName = messageData.Type == MessageBubbleType.User
				? "User"
				: (string.IsNullOrWhiteSpace(messageData.SenderName) ? DefaultCharacterDisplayName : messageData.SenderName.Trim());

			SendRequest(ChatRequests.PlayMessageAudio, new ChatPlayMessageAudioRequestPayload
			{
				MessageId = messageData.MessageId,
				CharacterName = characterName,
				Text = baseText,
				Tone = tone,
				ForceReload = true,
				MessageIndex = messageData.MessageIndex,
			});
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
				SendRequest(ChatRequests.GenerateReplyFromHistory, new ChatSendRequestPayload
				{
					SessionId = string.IsNullOrWhiteSpace(_sessionId) ? null : _sessionId,
					Model = string.IsNullOrWhiteSpace(_modelOverride) ? null : _modelOverride,
				});
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

			if (_messageContainer != null && !string.IsNullOrWhiteSpace(playback.MessageId) && !string.IsNullOrWhiteSpace(playback.UpdatedText))
			{
				var updatedRawText = playback.UpdatedText.Trim();
				_messageContainer.UpdateMessageContent(
					playback.MessageId,
					ConvertVocabMarkupToRichText(updatedRawText),
					StripVocabMarkup(updatedRawText),
					updatedRawText,
					playback.UpdatedPinyin);
			}

			StartCoroutine(PlaySingleMessageAudio(playback));
		}

		private IEnumerator PlaySingleMessageAudio(ChatPlayMessageAudioPayload playback)
		{
			if (playback.AudioClip == null)
			{
				// Clear reload state if this was a force-reload request that failed.
				if (playback.ForceReload && playback.MessageIndex >= 0 && _messageContainer != null)
				{
					_messageContainer.SetMessageTtsReloading(playback.MessageIndex, false);
					_reloadingTtsMessageIndices.Remove(playback.MessageIndex);
				}
				yield break;
			}

			// Hide speaker button for the playing message
			if (_messageContainer != null && !string.IsNullOrWhiteSpace(playback.MessageId))
			{
				_messageContainer.SetMessageTtsPlaying(playback.MessageId, true);
			}

			yield return StartCoroutine(PlayCharacterVoiceAsync(playback.AudioClip));

			// Show speaker button again after playback finishes
			if (_messageContainer != null && !string.IsNullOrWhiteSpace(playback.MessageId))
			{
				_messageContainer.SetMessageTtsPlaying(playback.MessageId, false);
			}

			// Clear reload state after playback
			if (playback.ForceReload && playback.MessageIndex >= 0 && _messageContainer != null)
			{
				_messageContainer.SetMessageTtsReloading(playback.MessageIndex, false);
				_reloadingTtsMessageIndices.Remove(playback.MessageIndex);
			}
		}

		private void ClearConversationState()
		{
			StopAllCoroutines();
			StopRecordingIfNeeded();
			UnbindAudioInputGuard();
			_pendingAudioBase64 = null;
			_pendingAudioMessageId = null;
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

			if (_vocabPopupView != null)
			{
				_vocabPopupView.Hide();
			}
		}

		/// <summary>
		/// Converts **word** markup to TMP rich text with underline and link tags.
		/// Example: 我**爱**你 → 我<u><link="vocab:爱">爱</link></u>你
		/// </summary>
		/// <param name="text">Raw text with ** markup.</param>
		/// <returns>TMP-compatible rich text string.</returns>
		private static string ConvertVocabMarkupToRichText(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return text;
			}

			return VocabMarkupRegex.Replace(text, match =>
			{
				var word = match.Groups[1].Value;
				return "<u><link=\"vocab:" + word + "\">" + word + "</link></u>";
			});
		}

		/// <summary>
		/// Strips **word** markup from text, keeping only the word itself.
		/// Used for TTS to ensure clean pronunciation.
		/// </summary>
		/// <param name="text">Raw text with ** markup.</param>
		/// <returns>Clean text with ** removed.</returns>
		private static string StripVocabMarkup(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return text;
			}

			return VocabMarkupRegex.Replace(text, "$1");
		}

		/// <summary>
		/// Handles vocab word click from message bubble.
		/// </summary>
		/// <param name="word">The Chinese word that was clicked.</param>
		private void HandleVocabWordClicked(string word)
		{
			if (string.IsNullOrWhiteSpace(word))
			{
				return;
			}

			if (_vocabPopupView != null)
			{
				_vocabPopupView.ShowLoading(word);
			}

			SendRequest(ChatRequests.LookupVocabulary, new ChatVocabLookupRequestPayload
			{
				Word = word
			});
		}

		/// <summary>
		/// Handles vocabulary lookup result from controller.
		/// </summary>
		/// <param name="payload">Vocab lookup result payload.</param>
		[OnEvent(ChatEvents.VocabLookupCompleted)]
		private void OnVocabLookupCompleted(object payload)
		{
			var result = payload as ChatVocabLookupResultPayload;
			if (result == null)
			{
				return;
			}

			if (_vocabPopupView != null)
			{
				_vocabPopupView.ShowResult(result.Id, result.Word, result.Pinyin, result.Vietnamese);
			}
		}

		/// <summary>
		/// Handles vocabulary review completion from controller.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(ChatEvents.VocabReviewCompleted)]
		private void OnVocabReviewCompleted(object payload)
		{
			if (_vocabPopupView != null)
			{
				_vocabPopupView.Hide();
			}
		}

		/// <summary>
		/// Handles learned vocabulary count loaded from controller.
		/// </summary>
		/// <param name="payload">Count payload.</param>
		[OnEvent(ChatEvents.VocabularyLearnedCountLoaded)]
		private void OnVocabularyLearnedCountLoaded(object payload)
		{
			var countPayload = payload as ChatVocabCountPayload;
			if (_countVocabText == null)
			{
				return;
			}

			var count = countPayload != null ? Mathf.Max(0, countPayload.Count) : 0;
			_countVocabText.text = count.ToString();
		}

		/// <summary>
		/// Re-loads learned vocabulary count after popup closes.
		/// </summary>
		private void HandleVocabPopupClosed()
		{
			RefreshVocabularyLearnedCount();
		}

		/// <summary>
		/// Handles vocab review rating from the popup and forwards to controller.
		/// </summary>
		/// <param name="vocabularyId">Reviewed vocabulary id.</param>
		/// <param name="rating">FSRS rating value.</param>
		private void HandleVocabReviewRequested(string vocabularyId, int rating)
		{
			if (string.IsNullOrWhiteSpace(vocabularyId))
			{
				return;
			}

			SendRequest(ChatRequests.ReviewVocabulary, new ChatVocabReviewRequestPayload
			{
				VocabularyId = vocabularyId,
				Rating = rating
			});
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
