using Features.GamePlay.SubFeatures.Chat.Events;
using Features.GamePlay.SubFeatures.Chat.Infrastructure;
using Features.GamePlay.SubFeatures.Chat.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Chat.Model;
using Features.GamePlay.SubFeatures.Chat.Requests;
using Core.Infrastructure.Network;
using Core.Infrastructure.State;
using Newtonsoft.Json;
using CoreGlobalModes = Core.Infrastructure.State.GlobalModes;
using System;
using System.Collections.Generic;
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
			EventBus.Publish(ChatEvents.Installed, null);
		}

		/// <summary>
		/// Uninstalls the subcontroller and clears bound parent signals.
		/// </summary>
		public static void Uninstall()
		{
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

			if (string.IsNullOrWhiteSpace(payload.Message))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Message is required."
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

			EventBus.Publish(ChatEvents.MessageAudioPlayRequested, new ChatPlayMessageAudioPayload
			{
				MessageId = payload.MessageId,
				CharacterName = characterName,
				Text = payload.Text,
				Tone = payload.Tone,
				VoiceName = voiceName,
				Pitch = pitch,
				SpeakingRate = speakingRate,
			});
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
			_ = EndConversationInternalAsync();
		}

		private static async Task<List<ChatSelectableCharacterPayload>> BuildSelectableCharactersAsync()
		{
			var result = new List<ChatSelectableCharacterPayload>();
			var names = ChatState.ParentSignals?.GetCharacterNames?.Invoke();
			if (names == null || names.Count == 0)
			{
				return result;
			}

			var activeNames = await GetActiveCharacterNamesAsync();

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

		private static async Task<HashSet<string>> GetActiveCharacterNamesAsync()
		{
			var activeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			try
			{
				var responseJson = await HttpClient.GetTaskAsync(GetChatDeveloperStateEndpoint());
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					return activeNames;
				}

				var response = JsonConvert.DeserializeObject<ChatDeveloperStatePayload>(responseJson);
				if (response?.ActiveCharacterNames == null || response.ActiveCharacterNames.Count == 0)
				{
					return activeNames;
				}

				for (var i = 0; i < response.ActiveCharacterNames.Count; i++)
				{
					var name = response.ActiveCharacterNames[i];
					if (string.IsNullOrWhiteSpace(name))
					{
						continue;
					}

					activeNames.Add(name.Trim());
				}
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[ChatController] Failed to load active character names: " + exception.Message);
			}

			return activeNames;
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

				EventBus.Publish(ChatEvents.MessageReceived, new ChatAssistantMessagePayload
				{
					Reply = response.Reply,
					Model = response.Model,
					SessionId = payload.SessionId,
					Turns = ParseAssistantTurns(response.Reply),
				});
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

				EventBus.Publish(ChatEvents.MessageReceived, new ChatAssistantMessagePayload
				{
					Reply = response.Reply,
					Model = response.Model,
					SessionId = payload?.SessionId,
					Turns = ParseAssistantTurns(response.Reply),
				});
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
	}
}
