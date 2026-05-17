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
using UnityEngine.EventSystems;
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
		private const string AutoChatContextTemplate = "AI tự nói chuyện khoảng {0} tin nhắn mỗi lượt. Các nhân vật không được phép ngủ. Yêu cầu: Phải diễn đúng theo context, không được nhảy cóc nội dung và không được diễn sai mục đích.";

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
		[SerializeField]
		private bool _isBlackUI;
		[SerializeField]
		private GameObject _topMenu;
		[SerializeField]
		private GameObject _bottomMenu;
		[SerializeField]
		private GameObject _body;
		[SerializeField]
		private TMP_InputField _inputNumberAutochat;
		[SerializeField]
		private Button _buttonApplyAutoChat;

		[SerializeField] 
		private ChatMissionView _chatMissionView;

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
		private bool _isVocabAudioRequestInProgress;
		private string _recordingDeviceName;
		private AudioClip _recordingAudioClip;
		private Image _recordButtonImage;
		private Color _recordButtonIdleColor = Color.white;
		private bool _hasCapturedRunInBackground;
		private bool _previousRunInBackground;
		private bool _isBackgroundRuntimeActive;
		private bool _hasCapturedVoiceIgnoreListenerPause;
		private bool _previousVoiceIgnoreListenerPause;
		private bool _hasSceneCharacters;
		private bool _isAutoChatEnabled;
		private bool _isAutoChatAwaitingReply;
		private bool _isAutoChatAwaitingApply;
		private bool _hasSentAutoChatContext;
		private int _autoChatTargetTurnCount = 10;
		private int _autoChatGeneratedTurnCount;
		private bool _isAutoChatBatchGenerating;
		private readonly List<ChatAssistantTurnPayload> _autoChatBatchBuffer = new List<ChatAssistantTurnPayload>();
		private const int MaxAutoChatNewVocabPerDay = 10;
		private const int MaxAutoChatOldVocabPool = 100;
		private const int AutoChatNewVocabWordsPerTurn = 3;
		private const int AutoChatOldVocabWordsPerTurn = 5;
		private readonly List<string> _autoChatVocabPool = new List<string>();
		private int _autoChatVocabIndex;
		private int _autoChatVocabWordsPerTurn;
		private readonly List<string> _autoChatPendingVocabWords = new List<string>();
		private readonly HashSet<string> _autoChatUsedVocabWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private bool _isAutoChatVocabLoaded;
		private readonly List<string> _vocabReviewQueue = new List<string>();
		private int _vocabReviewIndex;
		private bool _isVocabReviewMode;
		private Vector2 _saveBodyOriginalAnchorMin;
		private Vector2 _saveBodyOriginalAnchorMax;
		private Vector2 _saveBodyOriginalOffsetMin;
		private Vector2 _saveBodyOriginalOffsetMax;

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
			StopAutoChatMode();
			StopRecordingIfNeeded();
			UnbindInputFieldEvents();
			UnbindRecordButtonEvents();
			UnbindApplyAutoChatButtonEvents();
			UnbindAudioInputGuard();
			RestoreForegroundRuntimeMode();
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

			// Check user text against mission vocabulary before sending.
			if (!hasAudio && !string.IsNullOrWhiteSpace(userTextForTracking))
			{
				CheckMissionVocabUsage(userTextForTracking);
			}

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
			ResetAutoChatInputUi();
			EnableBackgroundRuntimeMode();
			RefreshSceneCharacterState();
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
			if (_isBlackUI)
			{
				_topMenu.SetActive(false);
				_bottomMenu.SetActive(false);
				var r = _body != null ? _body.GetComponent<RectTransform>() : null;
				if (r != null)
				{
					_saveBodyOriginalAnchorMin = r.anchorMin;
					_saveBodyOriginalAnchorMax = r.anchorMax;
					_saveBodyOriginalOffsetMin = r.offsetMin;
					_saveBodyOriginalOffsetMax = r.offsetMax;

					r.anchorMin = new Vector2(0f, 0f);
					r.anchorMax = new Vector2(1f, 1f);

					// Keep current left/right spacing, force Bottom and Top to 0.
					r.offsetMin = new Vector2(r.offsetMin.x, 0f);
					r.offsetMax = new Vector2(r.offsetMax.x, 0f);
				}
			}
			gameObject.SetActive(true);
			EnsureDependencies();
			EnableBackgroundRuntimeMode();
			RefreshSceneCharacterState();
			SendRequest(ChatRequests.LoadDeveloperState);
			RefreshHistory();
			RefreshVocabularyLearnedCount();
			LoadMissionVocabulary();
		}

		/// <summary>
		/// Hides this subfeature view when its controller is uninstalled.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(ChatEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			if (_isBlackUI)
			{
				_topMenu.SetActive(true);
				_bottomMenu.SetActive(true);
				var r = _body != null ? _body.GetComponent<RectTransform>() : null;
				if (r != null)
				{
					r.anchorMin = _saveBodyOriginalAnchorMin;
					r.anchorMax = _saveBodyOriginalAnchorMax;
					r.offsetMin = _saveBodyOriginalOffsetMin;
					r.offsetMax = _saveBodyOriginalOffsetMax;
				}
			}
			StopAllCoroutines();
			StopAutoChatMode();
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
			_autoChatUsedVocabWords.Clear();
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
			RestoreForegroundRuntimeMode();
			ClearMissionVocabulary();
			gameObject.SetActive(false);
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			if (hasFocus || !_isBackgroundRuntimeActive)
			{
				return;
			}

			AudioListener.pause = false;
		}

		private void OnApplicationPause(bool pauseStatus)
		{
			if (!pauseStatus || !_isBackgroundRuntimeActive)
			{
				return;
			}

			AudioListener.pause = false;
		}

		/// <summary>
		/// Handles keyboard shortcuts for chat actions.
		/// Ctrl+A opens add-character popup, Ctrl+O opens context popup,
		/// Ctrl+E ends the conversation, Ctrl+R toggles auto chat mode.
		/// </summary>
		private void Update()
		{
			if (!_hasSceneCharacters)
			{
				ClearBlockedInputSelection();
			}

			if (!IsControlPressed())
			{
				return;
			}

			if (Input.GetKeyDown(KeyCode.A))
			{
				SendRequest(ChatRequests.OpenAddCharacterPopup);
				return;
			}

			if (Input.GetKeyDown(KeyCode.O))
			{
				OnContextInputRequested(null);
				return;
			}

			if (Input.GetKeyDown(KeyCode.E))
			{
				OnEndConversationRequested(null);
				return;
			}

			if (Input.GetKeyDown(KeyCode.R))
			{
				ToggleAutoChatMode();
			}
		}

		/// <summary>
		/// Returns true when either left or right Control key is pressed.
		/// </summary>
		/// <returns>True when Control is currently held down.</returns>
		private static bool IsControlPressed()
		{
			return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
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
		/// Toggles auto-chat mode when the parent menu item is clicked.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(ChatEvents.AutoChatToggleRequested)]
		private void OnAutoChatToggleRequested(object payload)
		{
			ToggleAutoChatMode();
		}

		/// <summary>
		/// Handles request from controller to end conversation.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(ChatEvents.EndConversationRequested)]
		private void OnEndConversationRequested(object payload)
		{
			// Save used vocab words for post-chat review before StopAutoChatMode clears them.
			_vocabReviewQueue.Clear();
			_vocabReviewIndex = 0;
			if (_autoChatUsedVocabWords.Count > 0)
			{
				foreach (var word in _autoChatUsedVocabWords)
				{
					_vocabReviewQueue.Add(word);
				}
			}

			StopAutoChatMode();

			// If we have words to review, start review mode first.
			// Otherwise, end the conversation on server immediately.
			if (_vocabReviewQueue.Count > 0)
			{
				StartVocabReviewMode();
			}
			else
			{
				SendRequest(ChatRequests.EndConversation);
			}
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
						Context = turn.Context,
						Message = ConvertVocabMarkupToRichText(text),
						OriginalMessage = StripVocabMarkup(text),
						RawVocabText = text,
						Translation = turn.Translation,
						Pinyin = turn.Pinyin,
						Tone = string.IsNullOrWhiteSpace(turn.Tone) ? DefaultTtsTone : turn.Tone.Trim(),
						Emotion = turn.Emotion,
						Intensity = turn.Intensity,
						Avatar = SendRequest<Sprite>(ChatRequests.GetCharacterAvatar, characterName),
					});
				}
			}

			_messageContainer.SetMessages(mapped);
			EnsureAutoChatTranslationsVisible();
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

			_isAutoChatAwaitingReply = false;

			var turns = response.Turns != null && response.Turns.Count > 0 ? response.Turns : new List<ChatAssistantTurnPayload>();

			// Batch generating phase: buffer turns silently without display or TTS playback.
			if (_isAutoChatBatchGenerating)
			{
				var turnCount = 0;
				for (var i = 0; i < turns.Count; i++)
				{
					if (turns[i] == null)
					{
						continue;
					}

					_autoChatBatchBuffer.Add(turns[i]);
					turnCount++;
				}

				// If no structured turns, count the raw reply as one turn.
				if (turnCount == 0 && !string.IsNullOrWhiteSpace(response.Reply))
				{
					_autoChatBatchBuffer.Add(new ChatAssistantTurnPayload
					{
						MessageId = Guid.NewGuid().ToString("N"),
						CharacterName = DefaultCharacterDisplayName,
						Text = response.Reply,
						Tone = DefaultTtsTone,
						IsAudioPreloadCompleted = true,
					});
					turnCount = 1;
				}

				_autoChatGeneratedTurnCount += turnCount;
				Debug.Log("[ChatView] Auto chat batch gen: " + _autoChatGeneratedTurnCount + "/" + _autoChatTargetTurnCount + " turns buffered.");

				// Check which vocab words AI actually used in this response.
				CheckAutoChatVocabUsage(turns, response.Reply);

				if (_autoChatGeneratedTurnCount >= _autoChatTargetTurnCount)
				{
					// Enough turns generated. Move buffer to queue and start playback.
					_isAutoChatBatchGenerating = false;
					SetAutoChatTargetInputVisible(false);
					for (var i = 0; i < _autoChatBatchBuffer.Count; i++)
					{
						_pendingCharacterTurns.Enqueue(_autoChatBatchBuffer[i]);
					}

					_autoChatBatchBuffer.Clear();
					SetCharacterRespondingState(true);

					if (!_isProcessingCharacterTurns)
					{
						StartCoroutine(ProcessCharacterTurnsSequentially());
					}
				}
				else
				{
					// Need more turns. Request the next generation.
					TryTriggerNextAutoChatTurn();
				}

				return;
			}

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
				TryTriggerNextAutoChatTurn();
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

				// If audio is not ready, trigger resolution now (sequential/JIT)
				// if (turn.AudioClip == null && !turn.IsAudioPreloadCompleted)
				// {
				// 	SendRequest(ChatRequests.ResolveTurnAudio, turn);
				// }

				// Wait until this turn finishes resolution so text is displayed together with ready audio.
				// while (turn.AudioClip == null && !turn.IsAudioPreloadCompleted)
				// {
				// 	yield return null;
				// }

				var characterMessage = new MessageBubbleData
				{
					MessageId = string.IsNullOrWhiteSpace(turn.MessageId) ? Guid.NewGuid().ToString("N") : turn.MessageId,
					Type = MessageBubbleType.Character,
					SenderName = characterName,
					Context = turn.Context,
					Message = ConvertVocabMarkupToRichText(messageText),
					OriginalMessage = StripVocabMarkup(messageText),
					RawVocabText = messageText,
					Translation = turn.Translation,
					Pinyin = turn.Pinyin,
					Tone = tone,
					Emotion = turn.Emotion,
					Intensity = turn.Intensity,
					Avatar = SendRequest<Sprite>(ChatRequests.GetCharacterAvatar, characterName),
				};

				_messageContainer.AddNewMessage(characterMessage);
				if (_isAutoChatEnabled && !characterMessage.IsTranslationExpanded && !string.IsNullOrWhiteSpace(characterMessage.Translation))
				{
					_messageContainer.ToggleMessageTranslation(characterMessage);
				}
				ScrollMessagesToBottom();

				// if (turn.AudioClip != null)
				// {
				// 	yield return StartCoroutine(PlayCharacterVoiceAsync(turn.AudioClip));
				// }
			}

			_isProcessingCharacterTurns = false;
			SetCharacterRespondingState(false);

			// If batch playback just finished, stop auto chat entirely.
			if (_isAutoChatEnabled && !_isAutoChatBatchGenerating && _autoChatGeneratedTurnCount >= _autoChatTargetTurnCount)
			{
				StopAutoChatMode();
				yield break;
			}

			TryTriggerNextAutoChatTurn();
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

			if (_isBackgroundRuntimeActive && _characterVoiceAudioSource != null)
			{
				_characterVoiceAudioSource.ignoreListenerPause = true;
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
				_vocabPopupView.SetClosedCallback(HandleVocabPopupClosed);
				_vocabPopupView.SetNextCallback(HandleVocabReviewNext);
				_vocabPopupView.SetAudioPlayCallback(HandleVocabAudioPlayRequested);
				_vocabPopupView.SetGenerateExampleCallback(HandleGenerateVocabExample);
				RefreshVocabCharacterOptions();
			}

			if (_recordButton != null)
			{
				_recordButtonImage = _recordButton.targetGraphic as Image;
			}

			BindRecordButtonEvents();
			BindApplyAutoChatButtonEvents();
			UpdateRecordButtonVisualState();

			SetChatInputInteractable(!_isCharacterResponding && _hasSceneCharacters);

			BindInputFieldEvents();
		}

		private void SetCharacterRespondingState(bool isResponding)
		{
			_isCharacterResponding = isResponding;
			SetChatInputInteractable(!isResponding && _hasSceneCharacters);
		}

		/// <summary>
		/// Re-checks whether the scene has at least one character and updates chat input state.
		/// Called on enable, install, and after character toggle.
		/// </summary>
		private void RefreshSceneCharacterState()
		{
			_hasSceneCharacters = HasAnySceneCharacter();
			if (!_hasSceneCharacters && _isAutoChatEnabled)
			{
				StopAutoChatMode();
			}
			SetChatInputInteractable(!_isCharacterResponding && _hasSceneCharacters);
		}

		/// <summary>
		/// Re-evaluates chat input state after a character is added or removed from the scene.
		/// </summary>
		/// <param name="payload">Developer state payload.</param>
		[OnEvent(ChatEvents.DeveloperStateLoaded)]
		private void OnDeveloperStateLoaded(object payload)
		{
			RefreshSceneCharacterState();
		}

		/// <summary>
		/// Keeps chat/network/audio running while app is unfocused when ChatView is active.
		/// </summary>
		private void EnableBackgroundRuntimeMode()
		{
			if (!_hasCapturedRunInBackground)
			{
				_previousRunInBackground = Application.runInBackground;
				_hasCapturedRunInBackground = true;
			}

			Application.runInBackground = true;
			_isBackgroundRuntimeActive = true;

			if (_characterVoiceAudioSource != null)
			{
				if (!_hasCapturedVoiceIgnoreListenerPause)
				{
					_previousVoiceIgnoreListenerPause = _characterVoiceAudioSource.ignoreListenerPause;
					_hasCapturedVoiceIgnoreListenerPause = true;
				}

				_characterVoiceAudioSource.ignoreListenerPause = true;
			}

			AudioListener.pause = false;
		}

		/// <summary>
		/// Restores runtime/audio behavior captured before ChatView enabled background mode.
		/// </summary>
		private void RestoreForegroundRuntimeMode()
		{
			if (!_isBackgroundRuntimeActive)
			{
				return;
			}

			if (_hasCapturedRunInBackground)
			{
				Application.runInBackground = _previousRunInBackground;
			}

			if (_characterVoiceAudioSource != null && _hasCapturedVoiceIgnoreListenerPause)
			{
				_characterVoiceAudioSource.ignoreListenerPause = _previousVoiceIgnoreListenerPause;
			}

			_isBackgroundRuntimeActive = false;
			_hasCapturedRunInBackground = false;
			_hasCapturedVoiceIgnoreListenerPause = false;
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
			SetInputFieldInteractable(_inputField, isInteractable);
			SetInputFieldInteractable(_intputChat, isInteractable);

			if (!isInteractable)
			{
				ClearBlockedInputSelection();
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
		/// Applies interactable state to a TMP input field and immediately removes focus when disabled.
		/// </summary>
		/// <param name="inputField">Input field to update.</param>
		/// <param name="isInteractable">True to allow focus/input; otherwise force disabled.</param>
		private static void SetInputFieldInteractable(TMP_InputField inputField, bool isInteractable)
		{
			if (inputField == null)
			{
				return;
			}

			inputField.interactable = isInteractable;
			if (!isInteractable)
			{
				inputField.DeactivateInputField();
			}
		}

		/// <summary>
		/// Clears EventSystem selection when one of the chat input fields is selected while chat is blocked.
		/// </summary>
		private void ClearBlockedInputSelection()
		{
			var eventSystem = EventSystem.current;
			if (eventSystem == null)
			{
				return;
			}

			var selectedObject = eventSystem.currentSelectedGameObject;
			if (selectedObject == null)
			{
				return;
			}

			var isInputSelected = (_inputField != null && selectedObject == _inputField.gameObject)
				|| (_intputChat != null && selectedObject == _intputChat.gameObject);

			if (isInputSelected)
			{
				eventSystem.SetSelectedGameObject(null);
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

		/// <summary>
		/// Loads all cached character names used by vocab pronunciation dropdown.
		/// </summary>
		/// <returns>Distinct non-empty character names.</returns>
		private List<string> GetAllCharacterNamesForVocab()
		{
			var names = SendRequest<List<string>>(ChatRequests.GetAllCharacterNames);
			var result = new List<string>();
			if (names == null || names.Count == 0)
			{
				return result;
			}

			for (var i = 0; i < names.Count; i++)
			{
				var name = names[i];
				if (string.IsNullOrWhiteSpace(name))
				{
					continue;
				}

				result.Add(name.Trim());
			}

			return result;
		}

		/// <summary>
		/// Refreshes character dropdown options in vocab popup.
		/// </summary>
		private void RefreshVocabCharacterOptions()
		{
			if (_vocabPopupView == null)
			{
				return;
			}

			_vocabPopupView.SetCharacterOptions(GetAllCharacterNamesForVocab());
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

		/// <summary>
		/// Binds click listener for the Apply Auto Chat button.
		/// </summary>
		private void BindApplyAutoChatButtonEvents()
		{
			if (_buttonApplyAutoChat == null)
			{
				return;
			}

			_buttonApplyAutoChat.onClick.RemoveListener(HandleApplyAutoChatClicked);
			_buttonApplyAutoChat.onClick.AddListener(HandleApplyAutoChatClicked);
		}

		/// <summary>
		/// Unbinds click listener for the Apply Auto Chat button.
		/// </summary>
		private void UnbindApplyAutoChatButtonEvents()
		{
			if (_buttonApplyAutoChat == null)
			{
				return;
			}

			_buttonApplyAutoChat.onClick.RemoveListener(HandleApplyAutoChatClicked);
		}

		/// <summary>
		/// Handles Apply Auto Chat button click.
		/// </summary>
		private void HandleApplyAutoChatClicked()
		{
			if (_isAutoChatEnabled)
			{
				return;
			}

			if (!_isAutoChatAwaitingApply)
			{
				EnterAutoChatApplyMode();
				return;
			}

			StartAutoChatMode();
		}

		/// <summary>
		/// Shows and enables auto-chat target input, waiting for Apply.
		/// </summary>
		private void EnterAutoChatApplyMode()
		{
			_isAutoChatAwaitingApply = true;
			SetAutoChatTargetInputVisible(true);
			SetAutoChatTargetInputInteractable(true);

			if (_buttonApplyAutoChat != null)
			{
				_buttonApplyAutoChat.interactable = true;
			}

			if (_inputNumberAutochat != null)
			{
				_inputNumberAutochat.ActivateInputField();
				_inputNumberAutochat.Select();
			}
		}

		/// <summary>
		/// Resets auto-chat target input UI to idle state.
		/// </summary>
		private void ResetAutoChatInputUi()
		{
			_isAutoChatAwaitingApply = false;
			SetAutoChatTargetInputInteractable(true);
			SetAutoChatTargetInputVisible(false);

			if (_buttonApplyAutoChat != null)
			{
				_buttonApplyAutoChat.interactable = true;
			}
		}

		/// <summary>
		/// Shows or hides the auto-chat target input field.
		/// </summary>
		/// <param name="isVisible">True to show the input field.</param>
		private void SetAutoChatTargetInputVisible(bool isVisible)
		{
			if (_inputNumberAutochat == null)
			{
				return;
			}

			_inputNumberAutochat.gameObject.SetActive(isVisible);
		}

		/// <summary>
		/// Enables or disables editing for the auto-chat target input field.
		/// </summary>
		/// <param name="isInteractable">True to allow editing.</param>
		private void SetAutoChatTargetInputInteractable(bool isInteractable)
		{
			if (_inputNumberAutochat == null)
			{
				return;
			}

			_inputNumberAutochat.interactable = isInteractable;
			if (!isInteractable)
			{
				_inputNumberAutochat.DeactivateInputField();
			}
		}

		/// <summary>
		/// Parses the target turn count from the auto chat input field.
		/// Returns at least 1, defaults to 10 when input is empty or invalid.
		/// </summary>
		/// <returns>Clamped target turn count.</returns>
		private int ParseAutoChatTargetCount()
		{
			if (_inputNumberAutochat == null || string.IsNullOrWhiteSpace(_inputNumberAutochat.text))
			{
				return 10;
			}

			if (int.TryParse(_inputNumberAutochat.text.Trim(), out var parsed) && parsed >= 1)
			{
				return parsed;
			}

			return 10;
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
			SetChatInputInteractable(!_isCharacterResponding && _hasSceneCharacters);
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

		/// <summary>
		/// Called when the TTS service rewrites a message's text for audio compatibility.
		/// Updates the displayed message content to match the rewritten text.
		/// </summary>
		[OnEvent(ChatEvents.MessageContentUpdated)]
		private void OnMessageContentUpdated(object payload)
		{
			var updated = payload as ChatMessageContentUpdatedPayload;
			if (updated == null || string.IsNullOrWhiteSpace(updated.MessageId))
			{
				return;
			}

			if (_messageContainer == null)
			{
				return;
			}

			if (!string.IsNullOrWhiteSpace(updated.Text))
			{
				_messageContainer.UpdateMessageText(updated.MessageId, updated.Text);
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
				Emotion = messageData.Emotion,
				Intensity = messageData.Intensity,
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
				Emotion = messageData.Emotion,
				Intensity = messageData.Intensity,
			});
		}

		private void HandleMessageTranslateClicked(MessageBubbleData messageData)
		{
			if (_messageContainer == null || messageData == null)
			{
				return;
			}

			if (_isAutoChatEnabled)
			{
				if (!messageData.IsTranslationExpanded && !string.IsNullOrWhiteSpace(messageData.Translation))
				{
					_messageContainer.ToggleMessageTranslation(messageData);
				}

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
			var enrichedContext = context;
			if (!string.IsNullOrWhiteSpace(enrichedContext) && !enrichedContext.Contains("không được nhảy cóc"))
			{
				enrichedContext += "\n(Yêu cầu: Phải diễn đúng context, không được nhảy cóc và diễn sai mục đích)";
			}

			SendRequest(ChatRequests.SaveContext, new ChatSaveContextRequestPayload
			{
				SessionId = string.IsNullOrWhiteSpace(_sessionId) ? null : _sessionId,
				Context = enrichedContext,
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

		/// <summary>
		/// Toggles auto-chat mode. When enabled, AI keeps responding from history
		/// until the user toggles the mode off.
		/// </summary>
		private void ToggleAutoChatMode()
		{
			if (_isAutoChatEnabled)
			{
				StopAutoChatMode();
				return;
			}

			if (_isAutoChatAwaitingApply)
			{
				ResetAutoChatInputUi();
				return;
			}

			EnterAutoChatApplyMode();
		}

		/// <summary>
		/// Starts auto-chat mode and triggers the first turn using SaveAndSend
		/// with the fixed context "AI tự nói chuyện".
		/// </summary>
		private void StartAutoChatMode()
		{
			if (_isAutoChatEnabled)
			{
				return;
			}

			if (!HasAnySceneCharacter())
			{
				Debug.LogWarning("[ChatView] Auto chat is blocked because scene has no characters.", this);
				return;
			}

			_autoChatTargetTurnCount = ParseAutoChatTargetCount();
			_autoChatGeneratedTurnCount = 0;
			_isAutoChatAwaitingApply = false;
			_isAutoChatBatchGenerating = true;
			_autoChatBatchBuffer.Clear();
			_isAutoChatEnabled = true;
			_isAutoChatAwaitingReply = false;
			_hasSentAutoChatContext = false;
			SetAutoChatTargetInputInteractable(false);
			if (_buttonApplyAutoChat != null)
			{
				_buttonApplyAutoChat.interactable = false;
			}
			SetChatInputInteractable(false);
			Debug.Log("[ChatView] Auto chat batch started. Target: " + _autoChatTargetTurnCount + " turns.");
			EnsureAutoChatTranslationsVisible();
			SendRequest(ChatRequests.LoadAutoChatVocabulary);
		}

		private void EnsureAutoChatTranslationsVisible()
		{
			if (!_isAutoChatEnabled || _messageContainer == null)
			{
				return;
			}

			_messageContainer.SetCharacterTranslationsExpanded(true);
		}

		/// <summary>
		/// Stops auto-chat mode. In-flight reply requests are not cancelled,
		/// but no further turns will be requested.
		/// </summary>
		private void StopAutoChatMode()
		{
			var wasBatchGenerating = _isAutoChatBatchGenerating;
			_isAutoChatEnabled = false;
			_isAutoChatAwaitingReply = false;
			_isAutoChatAwaitingApply = false;
			_hasSentAutoChatContext = false;
			_isAutoChatBatchGenerating = false;
			_autoChatGeneratedTurnCount = 0;
			_autoChatBatchBuffer.Clear();
			ClearAutoChatVocabState();
			SetAutoChatTargetInputInteractable(true);
			SetAutoChatTargetInputVisible(false);
			if (_buttonApplyAutoChat != null)
			{
				_buttonApplyAutoChat.interactable = true;
			}

			// Re-enable chat input if we were in batch generation (input was disabled).
			if (wasBatchGenerating && !_isCharacterResponding)
			{
				SetChatInputInteractable(_hasSceneCharacters);
			}
		}

		/// <summary>
		/// Handles auto-chat vocabulary loaded from controller.
		/// Stores the due/new word pools and triggers the first auto-chat turn.
		/// </summary>
		/// <param name="payload">Auto-chat vocabulary payload.</param>
		[OnEvent(ChatEvents.AutoChatVocabularyLoaded)]
		private void OnAutoChatVocabularyLoaded(object payload)
		{
			if (!_isAutoChatEnabled)
			{
				return;
			}

			var vocabPayload = payload as ChatAutoChatVocabularyPayload;
			InitializeAutoChatVocabPool(vocabPayload);
			_isAutoChatVocabLoaded = true;
			Debug.Log("[ChatView] Auto chat vocab loaded. Pool size: " + _autoChatVocabPool.Count + ", per turn: " + _autoChatVocabWordsPerTurn + ", today new count: " + (vocabPayload?.TodayNewCount ?? 0));
			TryTriggerNextAutoChatTurn();
		}

		/// <summary>
		/// Initializes the auto-chat vocabulary pool from the loaded payload.
		/// Mode A (today's new < 10): pool of up to 10 new words, 3 per turn.
		/// Mode B (today's new >= 10): pool of up to 100 due/old words, 5 per turn.
		/// Mode C (no due words and daily new cap reached): empty pool, no insertion.
		/// Pool rotates back to start when exhausted.
		/// </summary>
		/// <param name="payload">Loaded vocabulary payload.</param>
		private void InitializeAutoChatVocabPool(ChatAutoChatVocabularyPayload payload)
		{
			_autoChatVocabPool.Clear();
			_autoChatVocabIndex = 0;
			_autoChatVocabWordsPerTurn = 0;
			_autoChatPendingVocabWords.Clear();

			if (payload == null)
			{
				return;
			}

			if (payload.TodayNewCount < MaxAutoChatNewVocabPerDay && payload.NewWords != null && payload.NewWords.Count > 0)
			{
				// Mode A: introduce new words, capped at 10 per day total.
				var capacity = Mathf.Min(payload.NewWords.Count, MaxAutoChatNewVocabPerDay);
				for (var i = 0; i < capacity; i++)
				{
					if (!string.IsNullOrWhiteSpace(payload.NewWords[i]))
					{
						_autoChatVocabPool.Add(payload.NewWords[i]);
					}
				}

				if (_autoChatVocabPool.Count > 0)
				{
					_autoChatVocabWordsPerTurn = AutoChatNewVocabWordsPerTurn;
					return;
				}
			}

			if (payload.DueWords != null && payload.DueWords.Count > 0)
			{
				// Mode B: review old/due words, capped at 100 in rotation.
				var capacity = Mathf.Min(payload.DueWords.Count, MaxAutoChatOldVocabPool);
				for (var i = 0; i < capacity; i++)
				{
					if (!string.IsNullOrWhiteSpace(payload.DueWords[i]))
					{
						_autoChatVocabPool.Add(payload.DueWords[i]);
					}
				}

				if (_autoChatVocabPool.Count > 0)
				{
					_autoChatVocabWordsPerTurn = AutoChatOldVocabWordsPerTurn;
				}
			}
			// Mode C: pool stays empty; chat continues without word injection.
		}

		/// <summary>
		/// Clears all auto-chat vocabulary state.
		/// </summary>
		private void ClearAutoChatVocabState()
		{
			_autoChatVocabPool.Clear();
			_autoChatVocabIndex = 0;
			_autoChatVocabWordsPerTurn = 0;
			_autoChatPendingVocabWords.Clear();
			_isAutoChatVocabLoaded = false;
		}

		/// <summary>
		/// Selects the next batch of vocabulary words for auto-chat context.
		/// Starts from carry-over (unused) words, then fills from the active pool.
		/// When the pool is exhausted, the index rotates back to the start.
		/// </summary>
		/// <returns>List of up to _autoChatVocabWordsPerTurn words.</returns>
		private List<string> SelectNextAutoChatVocabWords()
		{
			if (_autoChatVocabPool.Count == 0 || _autoChatVocabWordsPerTurn <= 0)
			{
				return new List<string>();
			}

			var selected = new List<string>(_autoChatPendingVocabWords);
			_autoChatPendingVocabWords.Clear();

			var safetyGuard = 0;
			while (selected.Count < _autoChatVocabWordsPerTurn)
			{
				if (_autoChatVocabIndex >= _autoChatVocabPool.Count)
				{
					_autoChatVocabIndex = 0;
				}

				selected.Add(_autoChatVocabPool[_autoChatVocabIndex]);
				_autoChatVocabIndex++;

				// Safety break: avoid infinite loop if pool is smaller than per-turn count.
				safetyGuard++;
				if (safetyGuard >= _autoChatVocabPool.Count && selected.Count >= _autoChatVocabPool.Count)
				{
					break;
				}
			}

			return selected;
		}

		/// <summary>
		/// Builds the vocabulary context string to include in auto-chat turn requests.
		/// Returns null when no vocabulary is available.
		/// </summary>
		/// <returns>Vocab instruction string, or null.</returns>
		private string BuildAutoChatVocabContext()
		{
			if (!_isAutoChatVocabLoaded)
			{
				return null;
			}

			var words = SelectNextAutoChatVocabWords();
			if (words.Count == 0)
			{
				return null;
			}

			// Store selected words so we can check usage later.
			_autoChatPendingVocabWords.Clear();
			for (var i = 0; i < words.Count; i++)
			{
				_autoChatPendingVocabWords.Add(words[i]);
			}

			return "";
		}

		/// <summary>
		/// Checks which pending vocabulary words were used by the AI in its response.
		/// Words not found in **word** markup are carried over to the next turn.
		/// </summary>
		/// <param name="turns">Structured turns from the response.</param>
		/// <param name="rawReply">Raw reply text.</param>
		private void CheckAutoChatVocabUsage(List<ChatAssistantTurnPayload> turns, string rawReply)
		{
			if (_autoChatPendingVocabWords.Count == 0)
			{
				return;
			}

			// Combine all text sources for checking.
			var combinedText = string.Empty;
			if (turns != null)
			{
				for (var i = 0; i < turns.Count; i++)
				{
					if (turns[i]?.Text != null)
					{
						combinedText += turns[i].Text;
					}
				}
			}

			if (!string.IsNullOrWhiteSpace(rawReply))
			{
				combinedText += rawReply;
			}

			var unusedWords = new List<string>();
			for (var i = 0; i < _autoChatPendingVocabWords.Count; i++)
			{
				var word = _autoChatPendingVocabWords[i];
				if (ContainsVocabWord(combinedText, word))
				{
					_autoChatUsedVocabWords.Add(word);
				}
				else
				{
					unusedWords.Add(word);
				}
			}

			_autoChatPendingVocabWords.Clear();
			for (var i = 0; i < unusedWords.Count; i++)
			{
				_autoChatPendingVocabWords.Add(unusedWords[i]);
			}

			if (unusedWords.Count > 0)
			{
				Debug.Log("[ChatView] Auto chat vocab carry-over: " + unusedWords.Count + " unused words.");
			}
		}

		/// <summary>
		/// Checks whether the reply text contains a vocabulary word, with or without **word** markup.
		/// </summary>
		/// <param name="text">Reply text to inspect.</param>
		/// <param name="word">Vocabulary word to find.</param>
		/// <returns>True when word is found in either format.</returns>
		private static bool ContainsVocabWord(string text, string word)
		{
			if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(word))
			{
				return false;
			}

			if (text.IndexOf("**" + word + "**", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}

			return text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		/// <summary>
		/// Requests the next auto-chat turn when mode is enabled and idle.
		/// The first turn is triggered by SaveAndSend with auto-chat context.
		/// Subsequent turns use GenerateReplyFromHistory.
		/// </summary>
		private void TryTriggerNextAutoChatTurn()
		{
			if (!_isAutoChatEnabled || _isAutoChatAwaitingReply)
			{
				return;
			}

			// During batch generation, skip the responding check since nothing is playing yet.
			if (!_isAutoChatBatchGenerating && _isCharacterResponding)
			{
				return;
			}

			if (!HasAnySceneCharacter())
			{
				Debug.LogWarning("[ChatView] Auto chat stopped because scene has no characters.", this);
				StopAutoChatMode();
				return;
			}

			_isAutoChatAwaitingReply = true;

			// Build vocab context for this turn.
			var vocabContext = BuildAutoChatVocabContext();
			Debug.Log(vocabContext);

			if (!_hasSentAutoChatContext)
			{
				_hasSentAutoChatContext = true;
				var fullContext = string.Format(AutoChatContextTemplate, Mathf.Min(_autoChatTargetTurnCount, 10));
				if (!string.IsNullOrEmpty(vocabContext))
				{
					fullContext += "\n" + vocabContext;
				}

				HandleSaveAndSendContextClicked(fullContext);
				return;
			}

			// For subsequent turns, update context with vocab words before requesting reply.
			if (!string.IsNullOrEmpty(vocabContext))
			{
				HandleSaveContextClicked(vocabContext);
			}

			SendRequest(ChatRequests.GenerateReplyFromHistory, new ChatSendRequestPayload
			{
				SessionId = string.IsNullOrWhiteSpace(_sessionId) ? null : _sessionId,
				Model = string.IsNullOrWhiteSpace(_modelOverride) ? null : _modelOverride,
			});
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
			if (_isVocabAudioRequestInProgress)
			{
				SetVocabAudioRequestInProgress(false);
			}

			var playback = payload as ChatPlayMessageAudioPayload;
			if (playback == null || string.IsNullOrWhiteSpace(playback.Text))
			{
				return;
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
			StopAutoChatMode();
			StopRecordingIfNeeded();
			UnbindAudioInputGuard();
			_pendingAudioBase64 = null;
			_pendingAudioMessageId = null;
			_pendingCharacterTurns.Clear();
			_isProcessingCharacterTurns = false;
			SetCharacterRespondingState(false);
			_reloadingTtsMessageIndices.Clear();
			_autoChatUsedVocabWords.Clear();

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
				SetVocabAudioRequestInProgress(false);
				RefreshVocabCharacterOptions();
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
				SetVocabAudioRequestInProgress(false);
				_vocabPopupView.ShowResult(result.Id, result.Word, result.Pinyin, result.Vietnamese);
				_vocabPopupView.gameObject.SetActive(true);
			}
		}

		/// <summary>
		/// Handles vocabulary review completion from controller.
		/// In review mode, advances to the next word automatically.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(ChatEvents.VocabReviewCompleted)]
		private void OnVocabReviewCompleted(object payload)
		{
			if (_isVocabReviewMode)
			{
				return;
			}

			if (_vocabPopupView != null)
			{
				_vocabPopupView.Hide();
			}
		}

		/// <summary>
		/// Handles the AI-generated vocabulary example sentence result from controller.
		/// Forwards the result to the vocab popup view for display.
		/// </summary>
		/// <param name="payload">Example result payload.</param>
		[OnEvent(ChatEvents.VocabExampleGenerated)]
		private void OnVocabExampleGenerated(object payload)
		{
			var result = payload as ChatVocabExampleResultPayload;
			if (result == null || _vocabPopupView == null)
			{
				return;
			}

			_vocabPopupView.ShowExampleSentence(result.Sentence, result.Pinyin, result.Translation);
		}

		/// <summary>
		/// Callback from vocab popup when the Example Sentences button is clicked.
		/// Triggers a request to generate a story-relevant example sentence.
		/// </summary>
		/// <param name="word">The vocabulary word to generate an example for.</param>
		private void HandleGenerateVocabExample(string word)
		{
			if (string.IsNullOrWhiteSpace(word))
			{
				return;
			}

			SendRequest(ChatRequests.GenerateVocabExample, new ChatVocabExampleRequestPayload
			{
				Word = word.Trim()
			});
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
		/// If in review mode and popup is closed early, finishes review and navigates home.
		/// </summary>
		private void HandleVocabPopupClosed()
		{
			RefreshVocabularyLearnedCount();

			if (_isVocabReviewMode)
			{
				FinishVocabReviewMode();
			}
		}

		/// <summary>
		/// Handles Next button in vocab review popup.
		/// Sends a single-word review for the current word, then advances to the next.
		/// </summary>
		private void HandleVocabReviewNext()
		{
			if (!_isVocabReviewMode)
			{
				return;
			}

			// Review the current word before advancing.
			if (_vocabReviewIndex < _vocabReviewQueue.Count)
			{
				var currentWord = _vocabReviewQueue[_vocabReviewIndex];
				SendRequest(ChatRequests.BatchReviewAutoChatVocabulary, new ChatBatchReviewVocabRequestPayload
				{
					Words = new System.Collections.Generic.List<string> { currentWord }
				});
			}

			_vocabReviewIndex++;
			if (_vocabReviewIndex < _vocabReviewQueue.Count)
			{
				ShowCurrentVocabReview();
			}
			else
			{
				if (_vocabPopupView != null)
				{
					_vocabPopupView.Hide();
				}

				FinishVocabReviewMode();
			}
		}

		/// <summary>
		/// Starts post-conversation vocabulary review mode.
		/// Shows vocab words one by one in the popup for the user to review.
		/// </summary>
		private void StartVocabReviewMode()
		{
			_isVocabReviewMode = true;
			_vocabReviewIndex = 0;

			Debug.Log("[ChatView] Starting vocab review mode with " + _vocabReviewQueue.Count + " words.");
			ShowCurrentVocabReview();

			if (_vocabPopupView != null)
			{
				_vocabPopupView.SetNextButtonVisible(true);
			}
		}

		/// <summary>
		/// Shows the current vocabulary word in the review popup via server lookup.
		/// </summary>
		private void ShowCurrentVocabReview()
		{
			if (_vocabReviewIndex >= _vocabReviewQueue.Count)
			{
				return;
			}

			var word = _vocabReviewQueue[_vocabReviewIndex];
			if (_vocabPopupView != null)
			{
				SetVocabAudioRequestInProgress(false);
				RefreshVocabCharacterOptions();
				_vocabPopupView.ShowLoading(word);
			}

			SendRequest(ChatRequests.LookupVocabulary, new ChatVocabLookupRequestPayload
			{
				Word = word
			});
		}

		/// <summary>
		/// Ends vocab review mode, clears review queue, and navigates home.
		/// </summary>
		private void FinishVocabReviewMode()
		{
			_isVocabReviewMode = false;
			_vocabReviewQueue.Clear();
			_vocabReviewIndex = 0;

			if (_vocabPopupView != null)
			{
				_vocabPopupView.SetNextButtonVisible(false);
			}

			Debug.Log("[ChatView] Vocab review mode finished. Now ending conversation on server.");

			// After review is finished, finalize the conversation (summarize) on server.
			SendRequest(ChatRequests.EndConversation);
		}

		/// <summary>
		/// Plays pronunciation for the selected vocabulary word using selected character voice.
		/// </summary>
		/// <param name="word">Vocabulary word to pronounce.</param>
		/// <param name="characterName">Selected character name from dropdown.</param>
		/// <param name="forceReload">Whether to force reload the TTS audio.</param>
		private void HandleVocabAudioPlayRequested(string word, string characterName, bool forceReload)
		{
			if (string.IsNullOrWhiteSpace(word))
			{
				return;
			}

			var selectedCharacterName = ResolveVocabAudioCharacterName(characterName);
			if (string.IsNullOrWhiteSpace(selectedCharacterName))
			{
				Debug.LogWarning("[ChatView] Cannot play vocab audio because no character is available.", this);
				return;
			}

			SetVocabAudioRequestInProgress(true);

			SendRequest(ChatRequests.PlayMessageAudio, new ChatPlayMessageAudioRequestPayload
			{
				CharacterName = selectedCharacterName,
				Text = StripVocabMarkup(word.Trim()),
				Tone = DefaultTtsTone,
				ForceReload = forceReload
			});
		}

		/// <summary>
		/// Resolves selected character name, with fallback to first available scene character.
		/// </summary>
		/// <param name="selectedCharacterName">Character name selected in dropdown.</param>
		/// <returns>Character name for TTS request, or null when unavailable.</returns>
		private string ResolveVocabAudioCharacterName(string selectedCharacterName)
		{
			if (!string.IsNullOrWhiteSpace(selectedCharacterName))
			{
				return selectedCharacterName.Trim();
			}

			var availableNames = GetAllCharacterNamesForVocab();
			if (availableNames.Count > 0)
			{
				return availableNames[0];
			}

			return null;
		}

		/// <summary>
		/// Logs chat request failures.
		/// </summary>
		/// <param name="payload">Error payload.</param>
		[OnEvent(ChatEvents.RequestFailed)]
		private void OnRequestFailed(object payload)
		{
			if (_isVocabAudioRequestInProgress)
			{
				SetVocabAudioRequestInProgress(false);
			}

			if (_isAutoChatEnabled && (_isAutoChatAwaitingReply || _isAutoChatBatchGenerating))
			{
				Debug.LogWarning("[ChatView] Auto chat stopped because request failed.", this);
				StopAutoChatMode();
			}

			var error = payload as ChatErrorPayload;
			Debug.LogWarning("[ChatView] Chat request failed: " + (error?.Message ?? "Unknown error"), this);
		}

		/// <summary>
		/// Updates vocab-audio request progress state in popup UI.
		/// </summary>
		/// <param name="isInProgress">True while waiting for server TTS response.</param>
		private void SetVocabAudioRequestInProgress(bool isInProgress)
		{
			_isVocabAudioRequestInProgress = isInProgress;
			if (_vocabPopupView != null)
			{
				_vocabPopupView.SetAudioRequestInProgress(isInProgress);
			}
		}

		#region Mission Vocabulary

		/// <summary>
		/// Requests due vocabulary from server for the mission panel.
		/// </summary>
		private void LoadMissionVocabulary()
		{
			SendRequest(ChatRequests.LoadMissionVocabulary);
		}

		/// <summary>
		/// Handles mission vocabulary loaded from controller.
		/// Populates the ChatMissionView with due vocabulary items.
		/// </summary>
		/// <param name="payload">Mission vocabulary payload.</param>
		[OnEvent(ChatEvents.MissionVocabularyLoaded)]
		private void OnMissionVocabularyLoaded(object payload)
		{
			if (_chatMissionView == null)
			{
				return;
			}

			var missionPayload = payload as ChatMissionVocabularyPayload;
			if (missionPayload == null || missionPayload.Items == null || missionPayload.Items.Count == 0)
			{
				_chatMissionView.ClearAll();
				_chatMissionView.gameObject.SetActive(false);
				return;
			}

			var entries = new List<MissionVocabEntry>();
			for (var i = 0; i < missionPayload.Items.Count; i++)
			{
				var item = missionPayload.Items[i];
				if (item == null || string.IsNullOrWhiteSpace(item.Korean))
				{
					continue;
				}

				entries.Add(new MissionVocabEntry
				{
					Hanzi = item.Korean.Trim(),
					Pinyin = item.Pinyin,
					Vietnamese = item.Vietnamese,
				});
			}

			if (entries.Count > 0)
			{
				_chatMissionView.gameObject.SetActive(true);
				_chatMissionView.SetMissionItems(entries);
			}
			else
			{
				_chatMissionView.ClearAll();
				_chatMissionView.gameObject.SetActive(false);
			}
		}

		/// <summary>
		/// Checks user text against mission vocabulary words.
		/// Marks matching words as completed (strikethrough) and moves them to the bottom.
		/// </summary>
		/// <param name="userText">User message text.</param>
		private void CheckMissionVocabUsage(string userText)
		{
			if (_chatMissionView == null || string.IsNullOrWhiteSpace(userText))
			{
				return;
			}

			_chatMissionView.CheckAndMarkUsedWords(userText);
		}

		/// <summary>
		/// Clears mission vocabulary state.
		/// </summary>
		private void ClearMissionVocabulary()
		{
			if (_chatMissionView != null)
			{
				_chatMissionView.ClearAll();
			}
		}

		#endregion
	}
}
