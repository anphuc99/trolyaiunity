using Features.GamePlay.SubFeatures.Chat.Events;
using Features.GamePlay.SubFeatures.Chat.Infrastructure;
using Features.GamePlay.SubFeatures.Chat.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Chat.Model;
using Features.GamePlay.SubFeatures.Chat.Requests;
using Core.Infrastructure.Network;
using Core.Infrastructure.State;
using Newtonsoft.Json;
using Share.Utils;
using CoreGlobalModes = Core.Infrastructure.State.GlobalModes;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Chat.Controller
{
	/// <summary>
	/// Controller for Chat.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class ChatController
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
		/// Installs the subcontroller.
		/// </summary>
		public static void Install()
		{
			RegisterAddCharacterMenu();
			RegisterContextMenu();
			RegisterEndConversationMenu();
			_ = LoadDeveloperStateInternalAsync();
			EventBus.Publish(ChatEvents.Installed, null);
		}

		/// <summary>
		/// Uninstalls the subcontroller and clears bound parent signals.
		/// </summary>
		public static void Uninstall()
		{
			// Fire-and-forget: trigger post-session relationship evaluation
			_ = TriggerRelationshipEvaluationAsync();

			UnregisterAddCharacterMenu();
			UnregisterContextMenu();
			UnregisterEndConversationMenu();
			// Clear API mode so the next chat session starts fresh (default mode).
			GlobalVariables.Remove(CoreGlobalModes.ChatApiModeKey);
			EventBus.Publish(ChatEvents.Uninstalled, null);
		}

		/// <summary>
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(ChatParentSignals signals)
		{
			if (signals == null)
			{
				UnregisterAddCharacterMenu();
				UnregisterContextMenu();
				UnregisterEndConversationMenu();
			}

			ChatState.ParentSignals = signals;
		}

		/// <summary>
		/// Loads chat history from server and publishes it to views.
		/// </summary>
		/// <param name="payload">Optional history request payload.</param>
		[Request(ChatRequests.LoadHistory)]
		public static void HandleLoadHistory(ChatHistoryRequestPayload payload)
		{
			_ = LoadHistoryInternalAsync(payload ?? new ChatHistoryRequestPayload());
		}

		/// <summary>
		/// Sends one chat message to server.
		/// </summary>
		/// <param name="payload">Chat send request payload.</param>
		[Request(ChatRequests.SendMessage)]
		public static void HandleSendMessage(ChatSendRequestPayload payload)
		{
			if (payload == null)
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Missing send message payload."
				});
				return;
			}

			if (string.IsNullOrWhiteSpace(payload.Message) && string.IsNullOrWhiteSpace(payload.Audio))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Message or audio is required."
				});
				return;
			}

			_ = SendMessageInternalAsync(payload);
		}

		/// <summary>
		/// Requests one assistant reply from current history without appending a user message.
		/// </summary>
		/// <param name="payload">Optional payload for session/model/story context.</param>
		[Request(ChatRequests.GenerateReplyFromHistory)]
		public static void HandleGenerateReplyFromHistory(ChatSendRequestPayload payload)
		{
			_ = GenerateReplyFromHistoryInternalAsync(payload ?? new ChatSendRequestPayload());
		}

		/// <summary>
		/// Gets cached character avatar sprite from parent signal.
		/// </summary>
		/// <param name="payload">Character name payload.</param>
		/// <returns>Sprite when available; otherwise null.</returns>
		[Request(ChatRequests.GetCharacterAvatar)]
		public static Sprite HandleGetCharacterAvatar(object payload)
		{
			if (payload is not string characterName || string.IsNullOrWhiteSpace(characterName))
			{
				return null;
			}

			return ChatState.ParentSignals?.GetCharacterAvatarByName?.Invoke(characterName.Trim());
		}

		/// <summary>
		/// Gets cached character voice name from parent signal.
		/// </summary>
		/// <param name="payload">Character name payload.</param>
		/// <returns>Voice name when available; otherwise null.</returns>
		[Request(ChatRequests.GetCharacterVoiceName)]
		public static string HandleGetCharacterVoiceName(object payload)
		{
			if (payload is not string characterName || string.IsNullOrWhiteSpace(characterName))
			{
				return null;
			}

			return ChatState.ParentSignals?.GetCharacterVoiceNameByName?.Invoke(characterName.Trim());
		}

		/// <summary>
		/// Gets cached character pitch from parent signal.
		/// </summary>
		/// <param name="payload">Character name payload.</param>
		/// <returns>Pitch when available; otherwise null.</returns>
		[Request(ChatRequests.GetCharacterPitch)]
		public static float? HandleGetCharacterPitch(object payload)
		{
			if (payload is not string characterName || string.IsNullOrWhiteSpace(characterName))
			{
				return null;
			}

			return ChatState.ParentSignals?.GetCharacterPitchByName?.Invoke(characterName.Trim());
		}

		/// <summary>
		/// Gets cached character speaking rate from parent signal.
		/// </summary>
		/// <param name="payload">Character name payload.</param>
		/// <returns>Speaking rate when available; otherwise null.</returns>
		[Request(ChatRequests.GetCharacterSpeakingRate)]
		public static float? HandleGetCharacterSpeakingRate(object payload)
		{
			if (payload is not string characterName || string.IsNullOrWhiteSpace(characterName))
			{
				return null;
			}

			return ChatState.ParentSignals?.GetCharacterSpeakingRateByName?.Invoke(characterName.Trim());
		}

		/// <summary>
		/// Handles replay request from chat view and publishes playback event.
		/// </summary>
		/// <param name="payload">Replay request payload.</param>
		[Request(ChatRequests.PlayMessageAudio)]
		public static void HandlePlayMessageAudio(ChatPlayMessageAudioRequestPayload payload)
		{
			if (payload == null || string.IsNullOrWhiteSpace(payload.Text))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Missing message content for audio playback."
				});
				return;
			}

			_ = PlayMessageAudioInternalAsync(payload);
		}

		/// <summary>
		/// Handles speech-to-text transcription requests from the view.
		/// </summary>
		/// <param name="payload">Transcription request payload.</param>
		[Request(ChatRequests.TranscribeAudio)]
		public static void HandleTranscribeAudio(ChatTranscribeAudioRequestPayload payload)
		{
			if (payload == null || string.IsNullOrWhiteSpace(payload.AudioBase64))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Missing audio data for transcription."
				});
				return;
			}

			_ = TranscribeAudioInternalAsync(payload);
		}

		/// <summary>
		/// Handles toggle changes for popup character selection and syncs to developer-state history.
		/// </summary>
		/// <param name="payload">Toggle request payload.</param>
		[Request(ChatRequests.SetCharacterActive)]
		public static void HandleSetCharacterActive(ChatSetCharacterActiveRequestPayload payload)
		{
			if (payload == null || string.IsNullOrWhiteSpace(payload.CharacterName))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Character name is required when changing active state."
				});
				return;
			}

			_ = SetCharacterActiveInternalAsync(payload);
		}

		/// <summary>
		/// Loads developer-state from server and publishes it to views.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[Request(ChatRequests.LoadDeveloperState)]
		public static void HandleLoadDeveloperState(object payload)
		{
			_ = LoadDeveloperStateInternalAsync();
		}

		/// <summary>
		/// Handles save-context requests from popup and syncs context via developer message API.
		/// </summary>
		/// <param name="payload">Context payload.</param>
		[Request(ChatRequests.SaveContext)]
		public static void HandleSaveContext(ChatSaveContextRequestPayload payload)
		{
			if (payload == null || string.IsNullOrWhiteSpace(payload.Context))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Context is required when saving developer context."
				});
				return;
			}

			_ = SaveContextInternalAsync(payload);
		}

		/// <summary>
		/// Handles end-conversation request and syncs journal creation on server.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[Request(ChatRequests.EndConversation)]
		public static void HandleEndConversation(object payload)
		{
			_ = EndConversationInternalAsync();
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(ChatRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(ChatEvents.Echoed, payload);
			ChatState.ParentSignals?.OnEchoed?.Invoke(payload);
		}

		/// <summary>
		/// Handles vocabulary word lookup from chat popup.
		/// </summary>
		/// <param name="payload">Lookup request payload with word.</param>
		[Request(ChatRequests.LookupVocabulary)]
		public static void HandleLookupVocabulary(ChatVocabLookupRequestPayload payload)
		{
			if (payload == null || string.IsNullOrWhiteSpace(payload.Word))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Missing word for vocabulary lookup."
				});
				return;
			}

			_ = LookupVocabularyInternalAsync(payload);
		}

		/// <summary>
		/// Handles vocabulary review rating from chat popup.
		/// </summary>
		/// <param name="payload">Review request payload with vocabulary id and rating.</param>
		[Request(ChatRequests.ReviewVocabulary)]
		public static void HandleReviewVocabulary(ChatVocabReviewRequestPayload payload)
		{
			if (payload == null || string.IsNullOrWhiteSpace(payload.VocabularyId))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Missing vocabulary id for review."
				});
				return;
			}

			_ = ReviewVocabularyInternalAsync(payload);
		}

		/// <summary>
		/// Loads learned vocabulary count for chat header display.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[Request(ChatRequests.LoadVocabularyLearnedCount)]
		public static void HandleLoadVocabularyLearnedCount(object payload)
		{
			_ = LoadVocabularyLearnedCountInternalAsync();
		}

		private static void RegisterAddCharacterMenu()
		{
			UnregisterAddCharacterMenu();

			var menuId = ChatState.ParentSignals?.AddMenu?.Invoke("Thêm nhân vật", HandleOpenAddCharacterMenu);
			if (string.IsNullOrWhiteSpace(menuId))
			{
				return;
			}

			ChatState.AddCharacterMenuId = menuId;
		}

		private static void UnregisterAddCharacterMenu()
		{
			if (string.IsNullOrWhiteSpace(ChatState.AddCharacterMenuId))
			{
				ChatState.AddCharacterMenuId = null;
				return;
			}

			ChatState.ParentSignals?.RemoveMenu?.Invoke(ChatState.AddCharacterMenuId);
			ChatState.AddCharacterMenuId = null;
		}

		private static void RegisterContextMenu()
		{
			UnregisterContextMenu();

			var menuId = ChatState.ParentSignals?.AddMenu?.Invoke("Nhập bối cảnh", HandleOpenContextMenu);
			if (string.IsNullOrWhiteSpace(menuId))
			{
				return;
			}

			ChatState.ContextMenuId = menuId;
		}

		private static void RegisterEndConversationMenu()
		{
			UnregisterEndConversationMenu();

			var menuId = ChatState.ParentSignals?.AddMenu?.Invoke("Kết thúc hội thoại", HandleOpenEndConversationMenu);
			if (string.IsNullOrWhiteSpace(menuId))
			{
				return;
			}

			ChatState.EndConversationMenuId = menuId;
		}

		private static void UnregisterContextMenu()
		{
			if (string.IsNullOrWhiteSpace(ChatState.ContextMenuId))
			{
				ChatState.ContextMenuId = null;
				return;
			}

			ChatState.ParentSignals?.RemoveMenu?.Invoke(ChatState.ContextMenuId);
			ChatState.ContextMenuId = null;
		}

		private static void UnregisterEndConversationMenu()
		{
			if (string.IsNullOrWhiteSpace(ChatState.EndConversationMenuId))
			{
				ChatState.EndConversationMenuId = null;
				return;
			}

			ChatState.ParentSignals?.RemoveMenu?.Invoke(ChatState.EndConversationMenuId);
			ChatState.EndConversationMenuId = null;
		}

		private static async void HandleOpenAddCharacterMenu()
		{
			var payload = await BuildSelectableCharactersAsync();
			EventBus.Publish(ChatEvents.CharactersLoaded, payload);
		}

		private static void HandleOpenContextMenu()
		{
			EventBus.Publish(ChatEvents.ContextInputRequested, null);
		}

		private static void HandleOpenEndConversationMenu()
		{
			EventBus.Publish(ChatEvents.EndConversationRequested, null);
		}

		private static async Task<List<ChatSelectableCharacterPayload>> BuildSelectableCharactersAsync()
		{
			var result = new List<ChatSelectableCharacterPayload>();
			var names = ChatState.ParentSignals?.GetCharacterNames?.Invoke();
			if (names == null || names.Count == 0)
			{
				return result;
			}

			var state = await LoadDeveloperStatePayloadAsync();
			var activeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (state?.ActiveCharacterNames != null)
			{
				for (var i = 0; i < state.ActiveCharacterNames.Count; i++)
				{
					var n = state.ActiveCharacterNames[i];
					if (!string.IsNullOrWhiteSpace(n))
					{
						activeNames.Add(n.Trim());
					}
				}
			}

			for (var i = 0; i < names.Count; i++)
			{
				var name = names[i];
				if (string.IsNullOrWhiteSpace(name))
				{
					continue;
				}

				var normalizedName = name.Trim();
				result.Add(new ChatSelectableCharacterPayload
				{
					Name = normalizedName,
					Avatar = ChatState.ParentSignals?.GetCharacterAvatarByName?.Invoke(normalizedName),
					IsActive = activeNames.Contains(normalizedName),
				});
			}

			return result;
		}

		private static async Task<ChatDeveloperStatePayload> LoadDeveloperStatePayloadAsync()
		{
			try
			{
				var responseJson = await HttpClient.GetTaskAsync(GetChatDeveloperStateEndpoint());
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					return new ChatDeveloperStatePayload();
				}

				var response = JsonConvert.DeserializeObject<ChatDeveloperStatePayload>(responseJson);
				return response ?? new ChatDeveloperStatePayload();
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[ChatController] Failed to load developer state: " + exception.Message);
			}

			return new ChatDeveloperStatePayload();
		}

		private static async Task LoadDeveloperStateInternalAsync()
		{
			var state = await LoadDeveloperStatePayloadAsync();
			EventBus.Publish(ChatEvents.DeveloperStateLoaded, state ?? new ChatDeveloperStatePayload());
		}

		/// <summary>
		/// Sends a fire-and-forget POST to /api/character-relationships/evaluate-session
		/// with the currently active character names so the cheap AI pipeline can
		/// update relationship emotions and thoughts.
		/// </summary>
		private static async Task TriggerRelationshipEvaluationAsync()
		{
			try
			{
				var state = await LoadDeveloperStatePayloadAsync();
				if (state?.ActiveCharacterNames == null || state.ActiveCharacterNames.Count == 0)
				{
					return;
				}

				var activeNames = new List<string>();
				for (var i = 0; i < state.ActiveCharacterNames.Count; i++)
				{
					var n = state.ActiveCharacterNames[i];
					if (!string.IsNullOrWhiteSpace(n))
					{
						activeNames.Add(n.Trim());
					}
				}

				if (activeNames.Count == 0)
				{
					return;
				}

				var payload = new { activeCharacterNames = activeNames };
				await HttpClient.PostJsonTaskAsync(NetworkEndpoints.CharacterRelationshipsEvaluateSession, payload);
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[ChatController] Relationship evaluation trigger failed: " + exception.Message);
			}
		}

		private static async Task SetCharacterActiveInternalAsync(ChatSetCharacterActiveRequestPayload payload)
		{
			try
			{
				var characterName = payload.CharacterName.Trim();
				var characterInfo = ChatState.ParentSignals?.GetCharacterInfoByName?.Invoke(characterName);
				var characterAppearance = ChatState.ParentSignals?.GetCharacterAppearanceByName?.Invoke(characterName);
				var request = new ChatDeveloperMessageRequestPayload
				{
					SessionId = string.IsNullOrWhiteSpace(payload.SessionId) ? null : payload.SessionId.Trim(),
					Kind = payload.IsActive ? "character_added" : "character_removed",
					Character = new ChatDeveloperMessageCharacterPayload
					{
						Name = characterName,
						Age = characterInfo?.Age,
						Personality = characterInfo?.Description,
						Gender = characterInfo?.Gender,
						Appearance = characterAppearance,
					}
				};

				var responseJson = await HttpClient.PostJsonTaskAsync(GetChatDeveloperEndpoint(), request);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
					{
						Message = "Failed to sync character active state."
					});
				}

				await LoadDeveloperStateInternalAsync();
			}
			catch (Exception exception)
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Failed to sync character active state: " + exception.Message
				});
			}
		}

		private static async Task SaveContextInternalAsync(ChatSaveContextRequestPayload payload)
		{
			try
			{
				var request = new ChatDeveloperMessageRequestPayload
				{
					SessionId = string.IsNullOrWhiteSpace(payload.SessionId) ? null : payload.SessionId.Trim(),
					Kind = "context_update",
					Context = payload.Context.Trim(),
				};

				var responseJson = await HttpClient.PostJsonTaskAsync(GetChatDeveloperEndpoint(), request);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
					{
						Message = "Failed to save developer context."
					});
				}

				await LoadDeveloperStateInternalAsync();
			}
			catch (Exception exception)
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Failed to save developer context: " + exception.Message
				});
			}
		}

		private static async Task EndConversationInternalAsync()
		{
			try
			{
				var responseJson = await HttpClient.PostJsonTaskAsync<object>(GetChatEndEndpoint(), null);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
					{
						Message = "Failed to finalize conversation."
					});
					return;
				}

				ChatEndConversationResponsePayload response = null;
				try
				{
					response = JsonConvert.DeserializeObject<ChatEndConversationResponsePayload>(responseJson);
				}
				catch (Exception exception)
				{
					Debug.LogWarning("[ChatController] Failed to parse end-conversation response: " + exception.Message);
				}

				EventBus.Publish(ChatEvents.ConversationEnded, response);
				ChatState.ParentSignals?.OpenHome?.Invoke();
			}
			catch (Exception exception)
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Failed to finalize conversation: " + exception.Message
				});
			}
		}

		/// <summary>
		/// Performs history API call and publishes result.
		/// </summary>
		/// <param name="payload">History request payload.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task LoadHistoryInternalAsync(ChatHistoryRequestPayload payload)
		{
			try
			{
				var endpoint = BuildHistoryEndpoint(payload?.SessionId);
				var responseJson = await HttpClient.GetTaskAsync(endpoint);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
					{
						Message = "Empty history response from server."
					});
					return;
				}

				var response = JsonConvert.DeserializeObject<ChatHistoryResponsePayload>(responseJson) ?? new ChatHistoryResponsePayload();

				// Pre-parse assistant turns so the View does not need to duplicate parsing logic.
				if (response.Messages != null)
				{
					for (var i = 0; i < response.Messages.Count; i++)
					{
						var message = response.Messages[i];
						if (message == null)
						{
							continue;
						}

						if (string.Equals(message.Role, "assistant", System.StringComparison.OrdinalIgnoreCase))
						{
							message.Turns = ParseAssistantTurns(message.Content);
						}
					}
				}

				EventBus.Publish(ChatEvents.HistoryLoaded, response);
			}
			catch (Exception exception)
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Failed to load chat history: " + exception.Message
				});
			}
		}

		/// <summary>
		/// Performs send API call and publishes assistant reply.
		/// </summary>
		/// <param name="payload">Send payload.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task SendMessageInternalAsync(ChatSendRequestPayload payload)
		{
			try
			{
				var responseJson = await HttpClient.PostJsonTaskAsync(GetChatSendEndpoint(), payload);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
					{
						Message = "Empty chat response from server."
					});
					return;
				}

				var response = JsonConvert.DeserializeObject<ChatSendResponsePayload>(responseJson);
				if (response == null || string.IsNullOrWhiteSpace(response.Reply))
				{
					EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
					{
						Message = !string.IsNullOrWhiteSpace(response?.Message) ? response.Message : "Server did not return a reply."
					});
					return;
				}

				// If server returned a transcription for the audio, notify the view
				if (!string.IsNullOrWhiteSpace(response.Transcribe))
				{
					EventBus.Publish(ChatEvents.AudioRecordingTranscribed, new ChatAudioTranscribedPayload
					{
						Transcribe = response.Transcribe,
						UserMessageId = payload.AudioMessageId,
					});
				}

				var turns = ParseAssistantTurns(response.Reply);

				EventBus.Publish(ChatEvents.MessageReceived, new ChatAssistantMessagePayload
				{
					Reply = response.Reply,
					Model = response.Model,
					SessionId = payload.SessionId,
					Turns = turns,
					Transcribe = response.Transcribe,
				});

				_ = PreResolveTtsAudioUrlsAsync(turns);
			}
			catch (Exception exception)
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Failed to send chat message: " + exception.Message
				});
			}
		}

		/// <summary>
		/// Performs respond API call and publishes assistant reply without user turn.
		/// </summary>
		/// <param name="payload">Optional payload for session/model/story context.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task GenerateReplyFromHistoryInternalAsync(ChatSendRequestPayload payload)
		{
			try
			{
				var responseJson = await HttpClient.PostJsonTaskAsync(GetChatRespondEndpoint(), payload);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
					{
						Message = "Empty chat response from server."
					});
					return;
				}

				var response = JsonConvert.DeserializeObject<ChatSendResponsePayload>(responseJson);
				if (response == null || string.IsNullOrWhiteSpace(response.Reply))
				{
					EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
					{
						Message = !string.IsNullOrWhiteSpace(response?.Message) ? response.Message : "Server did not return a reply."
					});
					return;
				}

				var turns = ParseAssistantTurns(response.Reply);

				EventBus.Publish(ChatEvents.MessageReceived, new ChatAssistantMessagePayload
				{
					Reply = response.Reply,
					Model = response.Model,
					SessionId = payload?.SessionId,
					Turns = turns,
				});

				_ = PreResolveTtsAudioUrlsAsync(turns);
			}
			catch (Exception exception)
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Failed to generate chat reply from history: " + exception.Message
				});
			}
		}

		/// <summary>
		/// Builds history endpoint with optional session query parameter.
		/// </summary>
		/// <param name="sessionId">Optional session id.</param>
		/// <returns>Resolved endpoint path.</returns>
		private static string BuildHistoryEndpoint(string sessionId)
		{
			var historyEndpoint = GetChatHistoryEndpoint();

			if (string.IsNullOrWhiteSpace(sessionId))
			{
				return historyEndpoint;
			}

			return historyEndpoint + "?sessionId=" + Uri.EscapeDataString(sessionId.Trim());
		}

		private static string GetChatHistoryEndpoint()
		{
			return IsMyLogChatMode() ? NetworkEndpoints.MyLogChatHistory : NetworkEndpoints.ChatHistory;
		}

		private static string GetChatSendEndpoint()
		{
			return IsMyLogChatMode() ? NetworkEndpoints.MyLogChatSend : NetworkEndpoints.ChatSend;
		}

		private static string GetChatEndEndpoint()
		{
			return IsMyLogChatMode() ? NetworkEndpoints.MyLogChatEnd : NetworkEndpoints.JournalsEnd;
		}

		private static string GetChatRespondEndpoint()
		{
			return IsMyLogChatMode() ? NetworkEndpoints.MyLogChatSend : NetworkEndpoints.ChatRespond;
		}

		/// <summary>
		/// Resolves the developer-state endpoint based on the current API mode.
		/// </summary>
		/// <returns>Resolved endpoint path.</returns>
		private static string GetChatDeveloperStateEndpoint()
		{
			return IsMyLogChatMode() ? NetworkEndpoints.MyLogChatDeveloperState : NetworkEndpoints.ChatDeveloperState;
		}

		/// <summary>
		/// Resolves the developer message endpoint based on the current API mode.
		/// </summary>
		/// <returns>Resolved endpoint path.</returns>
		private static string GetChatDeveloperEndpoint()
		{
			return IsMyLogChatMode() ? NetworkEndpoints.MyLogChatDeveloper : NetworkEndpoints.ChatDeveloper;
		}

		private static bool IsMyLogChatMode()
		{
			var mode = GlobalVariables.GetOrDefault(GlobalModes.ChatApiModeKey, GlobalModes.ModeDefault);
			return string.Equals(mode, GlobalModes.ModeMyLog, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Parses assistant reply JSON into turn list.
		/// </summary>
		/// <param name="reply">Raw reply string from server.</param>
		/// <returns>List of parsed turns or empty list.</returns>
		private static List<ChatAssistantTurnPayload> ParseAssistantTurns(string reply)
		{
			if (string.IsNullOrWhiteSpace(reply))
			{
				return new List<ChatAssistantTurnPayload>();
			}

			try
			{
				var parsedList = JsonConvert.DeserializeObject<List<ChatAssistantTurnPayload>>(reply);
				if (parsedList != null && parsedList.Count > 0)
				{
					return parsedList;
				}

				var single = JsonConvert.DeserializeObject<ChatAssistantTurnPayload>(reply);
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
		/// Regex to match **word** vocabulary markup for stripping before TTS.
		/// </summary>
		private static readonly Regex VocabMarkupRegex = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);

		/// <summary>
		/// Pre-resolves TTS audio URLs for each turn so the View only needs to download audio clips.
		/// Strips **vocab** markup from text before sending to TTS.
		/// </summary>
		/// <param name="turns">Parsed turn list to enrich with AudioUrl.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task PreResolveTtsAudioUrlsAsync(List<ChatAssistantTurnPayload> turns)
		{
			if (turns == null || turns.Count == 0)
			{
				return;
			}

			for (var i = 0; i < turns.Count; i++)
			{
				var turn = turns[i];
				if (turn == null)
				{
					continue;
				}

				try
				{
					if (string.IsNullOrWhiteSpace(turn.Text))
					{
						continue;
					}

					var characterName = string.IsNullOrWhiteSpace(turn.CharacterName) ? "Mimi" : turn.CharacterName.Trim();
					var tone = string.IsNullOrWhiteSpace(turn.Tone) ? "neutral" : turn.Tone.Trim();
					var cleanText = VocabMarkupRegex.Replace(turn.Text, "$1");
					turn.AudioUrl = await ResolveTtsAudioUrlAsync(cleanText, tone, characterName);

					if (!string.IsNullOrWhiteSpace(turn.AudioUrl))
					{
						var audioType = AudioUrlUtils.ResolveAudioType(turn.AudioUrl);
						turn.AudioClip = await HttpClient.DownloadAudioClipTaskAsync(turn.AudioUrl, audioType);
					}
				}
				catch (Exception exception)
				{
					Debug.LogWarning("[ChatController] Failed to preload turn audio: " + exception.Message);
				}
				finally
				{
					turn.IsAudioPreloadCompleted = true;
				}
			}
		}

		/// <summary>
		/// Performs TTS API call and publishes playback event with resolved audio URL.
		/// </summary>
		/// <param name="payload">Replay request payload.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task PlayMessageAudioInternalAsync(ChatPlayMessageAudioRequestPayload payload)
		{
			try
			{
				var characterName = string.IsNullOrWhiteSpace(payload.CharacterName)
					? string.Empty
					: payload.CharacterName.Trim();

				var voiceName = string.IsNullOrWhiteSpace(characterName)
					? null
					: ChatState.ParentSignals?.GetCharacterVoiceNameByName?.Invoke(characterName);

				var pitch = string.IsNullOrWhiteSpace(characterName)
					? null
					: ChatState.ParentSignals?.GetCharacterPitchByName?.Invoke(characterName);

				var speakingRate = string.IsNullOrWhiteSpace(characterName)
					? null
					: ChatState.ParentSignals?.GetCharacterSpeakingRateByName?.Invoke(characterName);

				var audioUrl = await ResolveTtsAudioUrlAsync(payload.Text, payload.Tone, characterName, payload.ForceReload);

				AudioClip audioClip = null;
				if (!string.IsNullOrWhiteSpace(audioUrl))
				{
					var audioType = AudioUrlUtils.ResolveAudioType(audioUrl);
					audioClip = await HttpClient.DownloadAudioClipTaskAsync(audioUrl, audioType);
				}

				EventBus.Publish(ChatEvents.MessageAudioPlayRequested, new ChatPlayMessageAudioPayload
				{
					MessageId = payload.MessageId,
					CharacterName = characterName,
					Text = payload.Text,
					Tone = payload.Tone,
					VoiceName = voiceName,
					Pitch = pitch,
					SpeakingRate = speakingRate,
					AudioUrl = audioUrl,
					AudioClip = audioClip,
					ForceReload = payload.ForceReload,
					MessageIndex = payload.MessageIndex,
				});
			}
			catch (Exception exception)
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Failed to resolve TTS audio: " + exception.Message
				});
			}
		}

		/// <summary>
		/// Performs speech-to-text API call and publishes transcription result.
		/// </summary>
		/// <param name="payload">Transcription request payload.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task TranscribeAudioInternalAsync(ChatTranscribeAudioRequestPayload payload)
		{
			try
			{
				var requestPayload = new ChatSpeechToTextRequestPayload
				{
					Audio = payload.AudioBase64,
					Language = payload.Language,
				};

				var responseJson = await HttpClient.PostJsonTaskAsync(NetworkEndpoints.ChatTranscribe, requestPayload);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
					{
						Message = "Empty speech-to-text response from server."
					});
					EventBus.Publish(ChatEvents.TranscriptionCompleted, null);
					return;
				}

				var response = JsonConvert.DeserializeObject<ChatSpeechToTextResponsePayload>(responseJson);
				var transcript = response?.Transcript?.Trim();
				EventBus.Publish(ChatEvents.TranscriptionCompleted, transcript);
			}
			catch (Exception exception)
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Failed to transcribe audio: " + exception.Message
				});
				EventBus.Publish(ChatEvents.TranscriptionCompleted, null);
			}
		}

		/// <summary>
		/// Performs vocabulary word lookup via API and publishes result to views.
		/// </summary>
		/// <param name="payload">Lookup request payload.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task LookupVocabularyInternalAsync(ChatVocabLookupRequestPayload payload)
		{
			try
			{
				var word = payload.Word.Trim();
				var endpoint = NetworkEndpoints.VocabularyLookup + "?word=" + Uri.EscapeDataString(word);
				var responseJson = await HttpClient.GetTaskAsync(endpoint);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
					{
						Message = "Empty vocabulary lookup response."
					});
					return;
				}

				var response = JsonConvert.DeserializeObject<ChatVocabLookupResponsePayload>(responseJson);
				if (response == null)
				{
					EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
					{
						Message = "Failed to parse vocabulary lookup response."
					});
					return;
				}

				EventBus.Publish(ChatEvents.VocabLookupCompleted, new ChatVocabLookupResultPayload
				{
					Id = response.Id,
					Word = response.Korean,
					Vietnamese = response.Vietnamese,
					Pinyin = response.Pinyin,
					IsNew = response.IsNew
				});
			}
			catch (Exception exception)
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Failed to lookup vocabulary: " + exception.Message
				});
			}
		}

		/// <summary>
		/// Submits a vocabulary review rating via API and publishes completion.
		/// </summary>
		/// <param name="payload">Review request payload.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task ReviewVocabularyInternalAsync(ChatVocabReviewRequestPayload payload)
		{
			try
			{
				var endpoint = NetworkEndpoints.VocabularyReview + "/" + Uri.EscapeDataString(payload.VocabularyId) + "/review";
				var body = new { rating = payload.Rating };
				var responseJson = await HttpClient.PostJsonTaskAsync(endpoint, body);

				EventBus.Publish(ChatEvents.VocabReviewCompleted, null);
			}
			catch (Exception exception)
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Failed to review vocabulary: " + exception.Message
				});
			}
		}

		/// <summary>
		/// Loads learned vocabulary count via API and publishes result to views.
		/// </summary>
		/// <returns>Awaitable task.</returns>
		private static async Task LoadVocabularyLearnedCountInternalAsync()
		{
			try
			{
				var responseJson = await HttpClient.GetTaskAsync(NetworkEndpoints.VocabularyLearnedCount);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
					{
						Message = "Empty vocabulary learned-count response."
					});
					return;
				}

				var response = JsonConvert.DeserializeObject<ChatVocabCountPayload>(responseJson);
				if (response == null)
				{
					EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
					{
						Message = "Failed to parse vocabulary learned-count response."
					});
					return;
				}

				EventBus.Publish(ChatEvents.VocabularyLearnedCountLoaded, response);
			}
			catch (Exception exception)
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Failed to load learned vocabulary count: " + exception.Message
				});
			}
		}

		/// <summary>
		/// Calls the TTS endpoint and returns the resolved absolute audio URL.
		/// </summary>
		/// <param name="text">Text to synthesize.</param>
		/// <param name="tone">Tone hint.</param>
		/// <param name="characterName">Character name.</param>
		/// <param name="forceReload">Whether to force regeneration.</param>
		/// <returns>Absolute audio URL, or null on failure.</returns>
		private static async Task<string> ResolveTtsAudioUrlAsync(string text, string tone, string characterName, bool forceReload = false)
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

				var ttsResponse = JsonConvert.DeserializeObject<ChatTextToSpeechResponsePayload>(responseJson);
				var rawUrl = ttsResponse?.Url;
				if (string.IsNullOrWhiteSpace(rawUrl))
				{
					return null;
				}

				var settings = UnityEngine.Resources.Load<NetworkSettings>("NetworkSettings");
				var baseUrl = AudioUrlUtils.NormalizeServerBaseUrl(settings != null ? settings.BaseUrl : null);
				return AudioUrlUtils.ResolveAudioUrl(rawUrl, baseUrl);
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[ChatController] TTS resolution failed: " + exception.Message);
				return null;
			}
		}
	}
}
