using Features.GamePlay.SubFeatures.Chat.Events;
using Features.GamePlay.SubFeatures.Chat.Infrastructure;
using Features.GamePlay.SubFeatures.Chat.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Chat.Model;
using Features.GamePlay.SubFeatures.Chat.Requests;
using Core.Infrastructure.Network;
using Core.Infrastructure.State;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Share.Utils;
using CoreGlobalModes = Core.Infrastructure.State.GlobalModes;
using System;
using System.Collections.Generic;
using System.Text;
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
			ChatState.ActiveCharacterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Called when the controller scope is exited.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerShutdown]
		public static void OnExitScope()
		{
			ChatState.ActiveCharacterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Installs the subcontroller.
		/// </summary>
		public static void Install()
		{
			RegisterAddCharacterMenu();
			RegisterContextMenu();
			RegisterAutoChatMenu();
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
			UnregisterAutoChatMenu();
			UnregisterEndConversationMenu();
			ChatState.ActiveCharacterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
				UnregisterAutoChatMenu();
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

			if (!HandleHasAnySceneCharacter(null))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Chat is blocked because no active character exists in scene."
				});
				return;
			}

			_ = SendMessageInternalAsync(payload);
		}

		/// <summary>
		/// Checks whether current scene has at least one available character.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		/// <returns>True when at least one active character exists in current chat scene.</returns>
		[Request(ChatRequests.HasAnySceneCharacter)]
		public static bool HandleHasAnySceneCharacter(object payload)
		{
			if (ChatState.ActiveCharacterNames == null || ChatState.ActiveCharacterNames.Count == 0)
			{
				return false;
			}

			foreach (var name in ChatState.ActiveCharacterNames)
			{
				if (!string.IsNullOrWhiteSpace(name))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Gets normalized character names from parent cache for view dropdown binding.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		/// <returns>Distinct non-empty character names in original order.</returns>
		[Request(ChatRequests.GetAllCharacterNames)]
		public static List<string> HandleGetAllCharacterNames(object payload)
		{
			var names = ChatState.ParentSignals?.GetCharacterNames?.Invoke();
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

				var normalizedName = name.Trim();
				var alreadyAdded = false;
				for (var j = 0; j < result.Count; j++)
				{
					if (!string.Equals(result[j], normalizedName, StringComparison.Ordinal))
					{
						continue;
					}

					alreadyAdded = true;
					break;
				}

				if (!alreadyAdded)
				{
					result.Add(normalizedName);
				}
			}

			return result;
		}

		/// <summary>
		/// Requests one assistant reply from current history without appending a user message.
		/// </summary>
		/// <param name="payload">Optional payload for session/model/story context.</param>
		[Request(ChatRequests.GenerateReplyFromHistory)]
		public static void HandleGenerateReplyFromHistory(ChatSendRequestPayload payload)
		{
			if (!HandleHasAnySceneCharacter(null))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Chat is blocked because no active character exists in scene."
				});
				return;
			}

			_ = GenerateReplyFromHistoryInternalAsync(payload ?? new ChatSendRequestPayload());
		}

		/// <summary>
		/// Resolves TTS audio for a specific assistant turn.
		/// Used by View to trigger sequential JIT (Just-In-Time) audio generation.
		/// </summary>
		/// <param name="turn">The turn payload to resolve audio for.</param>
		[Request(ChatRequests.ResolveTurnAudio)]
		public static void HandleResolveTurnAudio(ChatAssistantTurnPayload turn)
		{
			if (turn == null) return;
			_ = ResolveTurnAudioInternalAsync(turn);
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
		/// Opens add-character popup by loading selectable characters and publishing CharactersLoaded.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[Request(ChatRequests.OpenAddCharacterPopup)]
		public static void HandleOpenAddCharacterPopup(object payload)
		{
			HandleOpenAddCharacterMenu();
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

		/// <summary>
		/// Loads due and all vocabulary lists for auto-chat word injection.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[Request(ChatRequests.LoadAutoChatVocabulary)]
		public static void HandleLoadAutoChatVocabulary(object payload)
		{
			_ = LoadAutoChatVocabularyInternalAsync();
		}

		/// <summary>
		/// Sends a list of vocabulary words to the server for batch review (mark as learned once).
		/// </summary>
		/// <param name="payload">Batch review request with word list.</param>
		[Request(ChatRequests.BatchReviewAutoChatVocabulary)]
		public static void HandleBatchReviewAutoChatVocabulary(ChatBatchReviewVocabRequestPayload payload)
		{
			if (payload == null || payload.Words == null || payload.Words.Count == 0)
			{
				return;
			}

			_ = BatchReviewAutoChatVocabularyInternalAsync(payload);
		}

		/// <summary>
		/// Handles vocabulary example sentence generation from chat popup.
		/// </summary>
		/// <param name="payload">Request payload with word.</param>
		[Request(ChatRequests.GenerateVocabExample)]
		public static void HandleGenerateVocabExample(ChatVocabExampleRequestPayload payload)
		{
			if (payload == null || string.IsNullOrWhiteSpace(payload.Word))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Missing word for example sentence generation."
				});
				return;
			}

			_ = GenerateVocabExampleInternalAsync(payload);
		}

		/// <summary>
		/// Loads due vocabulary items for the mission panel.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[Request(ChatRequests.LoadMissionVocabulary)]
		public static void HandleLoadMissionVocabulary(object payload)
		{
			_ = LoadMissionVocabularyInternalAsync();
		}

		private static async Task LoadMissionVocabularyInternalAsync()
		{
			try
			{
				var json = await HttpClient.GetTaskAsync(NetworkEndpoints.VocabularyDue);
				var result = new ChatMissionVocabularyPayload();

				if (!string.IsNullOrWhiteSpace(json))
				{
					var parsed = JsonConvert.DeserializeObject<JObject>(json);
					var vocabArray = parsed?["vocabularies"] as JArray;
					if (vocabArray != null)
					{
						for (var i = 0; i < vocabArray.Count; i++)
						{
							var item = vocabArray[i];
							var korean = item?["korean"]?.ToString();
							if (string.IsNullOrWhiteSpace(korean))
							{
								continue;
							}

							result.Items.Add(new ChatMissionVocabItemPayload
							{
								Korean = korean.Trim(),
								Pinyin = item?["pinyin"]?.ToString() ?? string.Empty,
								Vietnamese = item?["vietnamese"]?.ToString() ?? string.Empty,
							});
						}
					}
				}

				EventBus.Publish(ChatEvents.MissionVocabularyLoaded, result);
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[ChatController] Failed to load mission vocabulary: " + exception.Message);
				EventBus.Publish(ChatEvents.MissionVocabularyLoaded, new ChatMissionVocabularyPayload());
			}
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

		private static void RegisterAutoChatMenu()
		{
			UnregisterAutoChatMenu();

			var menuId = ChatState.ParentSignals?.AddMenu?.Invoke("Chat tự động", HandleToggleAutoChatMenu);
			if (string.IsNullOrWhiteSpace(menuId))
			{
				return;
			}

			ChatState.AutoChatMenuId = menuId;
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

		private static void UnregisterAutoChatMenu()
		{
			if (string.IsNullOrWhiteSpace(ChatState.AutoChatMenuId))
			{
				ChatState.AutoChatMenuId = null;
				return;
			}

			ChatState.ParentSignals?.RemoveMenu?.Invoke(ChatState.AutoChatMenuId);
			ChatState.AutoChatMenuId = null;
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

		private static void HandleToggleAutoChatMenu()
		{
			EventBus.Publish(ChatEvents.AutoChatToggleRequested, null);
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
			UpdateActiveCharacterNamesCache(state);

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
					IsActive = ChatState.ActiveCharacterNames.Contains(normalizedName),
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
			UpdateActiveCharacterNamesCache(state);
			EventBus.Publish(ChatEvents.DeveloperStateLoaded, state ?? new ChatDeveloperStatePayload());
		}

		private static void UpdateActiveCharacterNamesCache(ChatDeveloperStatePayload state)
		{
			var activeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (state?.ActiveCharacterNames != null)
			{
				for (var i = 0; i < state.ActiveCharacterNames.Count; i++)
				{
					var name = state.ActiveCharacterNames[i];
					if (!string.IsNullOrWhiteSpace(name))
					{
						activeNames.Add(name.Trim());
					}
				}
			}

			ChatState.ActiveCharacterNames = activeNames;
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

		/// <summary>
		/// Ends the conversation. On desktop platforms (non-MyLog mode), compresses history
		/// and uses local Ollama to summarize before sending the result to the server.
		/// Otherwise falls back to the server-side AI summarization.
		/// </summary>
		private static async Task EndConversationInternalAsync()
		{
			try
			{
				// // Desktop + non-MyLog: use local Ollama for summarization if selected
				if (IsDesktopPlatform() && IsOllamaSelected() && !IsMyLogChatMode())
				{
					await EndConversationViaLocalAIAsync();
					return;
				}

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
		/// Ends conversation on desktop using local Ollama for summarization.
		/// 1. Loads chat history from server.
		/// 2. Compresses history to CharacterName:Text format.
		/// 3. Sends compressed history to local Ollama for summarization.
		/// 4. Sends pre-computed summary to server via /api/journals/end-local.
		/// </summary>
		private static async Task EndConversationViaLocalAIAsync()
		{
			// Step 1: Load history from server
			var historyEndpoint = BuildHistoryEndpoint(null);
			var historyJson = await HttpClient.GetTaskAsync(historyEndpoint);
			if (string.IsNullOrWhiteSpace(historyJson))
			{
				Debug.LogWarning("[ChatController] Empty history for local summarization, falling back to server.");
				await EndConversationViaServerAsync();
				return;
			}

			var historyResponse = JsonConvert.DeserializeObject<ChatHistoryResponsePayload>(historyJson);
			if (historyResponse?.Messages == null || historyResponse.Messages.Count == 0)
			{
				Debug.LogWarning("[ChatController] No messages in history for local summarization, falling back to server.");
				await EndConversationViaServerAsync();
				return;
			}

			// Step 2: Compress history to CharacterName:Text format
			var compressedHistory = CompressHistoryForSummary(historyResponse.Messages);
			if (string.IsNullOrWhiteSpace(compressedHistory))
			{
				Debug.LogWarning("[ChatController] Compressed history is empty, falling back to server.");
				await EndConversationViaServerAsync();
				return;
			}

			Debug.Log("[ChatController] Compressed history for Ollama summarization:\n" + compressedHistory);

			// Step 3: Send compressed history to local Ollama
			var ollamaResponse = await OllamaService.SummarizeConversationAsync(compressedHistory);
			if (ollamaResponse?.Message == null || string.IsNullOrWhiteSpace(ollamaResponse.Message.Content))
			{
				Debug.LogWarning("[ChatController] Ollama summarization failed, falling back to server.");
				await EndConversationViaServerAsync();
				return;
			}

			// Step 4: Parse Ollama summary JSON
			var rawContent = ollamaResponse.Message.Content;
			var cleanedContent = NormalizePotentialOllamaJsonReply(rawContent);

			OllamaSummaryResult summaryResult = null;
			try
			{
				summaryResult = JsonConvert.DeserializeObject<OllamaSummaryResult>(cleanedContent);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[ChatController] Failed to parse Ollama summary JSON: " + ex.Message
					+ "\nRaw: " + rawContent
					+ "\nCleaned: " + cleanedContent);
			}

			// If JSON parsing fails, use the raw text as summary
			var summary = summaryResult?.Summary ?? cleanedContent;
			if (string.IsNullOrWhiteSpace(summary))
			{
				summary = rawContent;
			}
			var updatedStoryDescription = summaryResult?.UpdatedStoryDescription ?? "";

			// Step 5: Send pre-computed summary to server
			var localPayload = new ChatEndConversationLocalRequestPayload
			{
				Summary = summary,
				UpdatedStoryDescription = updatedStoryDescription,
			};

			var responseJson = await HttpClient.PostJsonTaskAsync(NetworkEndpoints.JournalsEndLocal, localPayload);
			if (string.IsNullOrWhiteSpace(responseJson))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Failed to save local AI summary to server."
				});
				return;
			}

			ChatEndConversationResponsePayload response = null;
			try
			{
				response = JsonConvert.DeserializeObject<ChatEndConversationResponsePayload>(responseJson);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[ChatController] Failed to parse end-local response: " + ex.Message);
			}

			EventBus.Publish(ChatEvents.ConversationEnded, response);
			ChatState.ParentSignals?.OpenHome?.Invoke();
		}

		/// <summary>
		/// Fallback: ends conversation via server-side AI summarization.
		/// </summary>
		private static async Task EndConversationViaServerAsync()
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
			catch (Exception ex)
			{
				Debug.LogWarning("[ChatController] Failed to parse end-conversation response: " + ex.Message);
			}

			EventBus.Publish(ChatEvents.ConversationEnded, response);
			ChatState.ParentSignals?.OpenHome?.Invoke();
		}

		/// <summary>
		/// Compresses chat history messages into a compact format for local AI summarization.
		/// Format: each line is "CharacterName:Text" for assistant turns, "User:Text" for user messages.
		/// Developer/system messages are excluded to reduce token count.
		/// </summary>
		/// <param name="messages">Chat history messages.</param>
		/// <returns>Compressed conversation string.</returns>
		public static string CompressHistoryForSummary(List<ChatHistoryMessagePayload> messages)
		{
			if (messages == null || messages.Count == 0)
			{
				return "";
			}

			var builder = new System.Text.StringBuilder();

			foreach (var message in messages)
			{
				if (message == null || string.IsNullOrWhiteSpace(message.Content))
				{
					continue;
				}

				var role = (message.Role ?? "").ToLowerInvariant();

				if (role == "user")
				{
					var text = message.Content.Trim();
					if (!string.IsNullOrWhiteSpace(text))
					{
						builder.AppendLine("User:" + text);
					}
				}
				else if (role == "assistant")
				{
					var turns = ParseAssistantTurns(message.Content);
					if (turns.Count > 0)
					{
						foreach (var turn in turns)
						{
							var charName = !string.IsNullOrWhiteSpace(turn.CharacterName)
								? turn.CharacterName.Trim()
								: "Mimi";
							var text = !string.IsNullOrWhiteSpace(turn.Text)
								? turn.Text.Trim()
								: "";
							if (!string.IsNullOrWhiteSpace(text))
							{
								builder.AppendLine(charName + ":" + text);
							}
						}
					}
					else
					{
						// Fallback: raw assistant content without JSON structure
						var text = message.Content.Trim();
						if (!string.IsNullOrWhiteSpace(text))
						{
							builder.AppendLine("Mimi:" + text);
						}
					}
				}
				// Skip developer and system messages to minimize tokens
			}

			return builder.ToString().TrimEnd();
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
							var parsedTurns = ParseAssistantTurns(message.Content);
							message.Turns = parsedTurns;
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
		/// On desktop platforms, uses local Ollama AI and saves history to server.
		/// </summary>
		/// <param name="payload">Send payload.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task SendMessageInternalAsync(ChatSendRequestPayload payload)
		{
			try
			{
				if (IsDesktopPlatform() && IsOllamaSelected() && !HasAudioPayload(payload))
				{
					await SendMessageViaLocalAIAsync(payload);
					return;
				}

				var savedModel = PlayerPrefs.GetString("SelectedModel", "gemini-flash-lite-latest");
				if (string.IsNullOrWhiteSpace(payload.Model))
				{
					payload.Model = savedModel;
				}

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

				// _ = PreResolveTtsAudioUrlsAsync(turns);
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
		/// On desktop platforms, uses local Ollama AI and saves history to server.
		/// </summary>
		/// <param name="payload">Optional payload for session/model/story context.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task GenerateReplyFromHistoryInternalAsync(ChatSendRequestPayload payload)
		{
			try
			{
				if (IsDesktopPlatform() && IsOllamaSelected())
				{
					await GenerateReplyFromHistoryViaLocalAIAsync(payload);
					return;
				}

				var savedModel = PlayerPrefs.GetString("SelectedModel", "gemini-flash-lite-latest");
				if (string.IsNullOrWhiteSpace(payload.Model))
				{
					payload.Model = savedModel;
				}

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

				// _ = PreResolveTtsAudioUrlsAsync(turns);
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
		/// Checks whether the current platform is a desktop PC (Windows, macOS, Linux).
		/// When true, local Ollama AI is preferred over server-side cloud AI.
		/// </summary>
		/// <returns>True on desktop editor or standalone builds.</returns>
		private static bool IsDesktopPlatform()
		{
			var platform = Application.platform;
			return platform == RuntimePlatform.WindowsEditor
				|| platform == RuntimePlatform.WindowsPlayer
				|| platform == RuntimePlatform.OSXEditor
				|| platform == RuntimePlatform.OSXPlayer
				|| platform == RuntimePlatform.LinuxEditor
				|| platform == RuntimePlatform.LinuxPlayer;
		}

		/// <summary>
		/// Checks whether the user has explicitly selected the Ollama model.
		/// </summary>
		/// <returns>True when Ollama is selected.</returns>
		private static bool IsOllamaSelected()
		{
			return string.Equals(PlayerPrefs.GetString("SelectedModel", "gemini-flash-lite-latest"), "ollama", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Checks whether the send payload contains audio data (audio requires server-side processing).
		/// </summary>
		/// <param name="payload">Send request payload.</param>
		/// <returns>True when audio is present.</returns>
		private static bool HasAudioPayload(ChatSendRequestPayload payload)
		{
			return payload != null && !string.IsNullOrWhiteSpace(payload.Audio);
		}

		/// <summary>
		/// Sends a user message using local Ollama AI on desktop platforms.
		/// 1. Requests system prompt + history from server via /api/chat/prepare-local.
		/// 2. Sends conversation to local Ollama to generate a reply.
		/// 3. Saves the user message + AI reply to server via /api/chat/save-local.
		/// 4. Publishes the result to views.
		/// </summary>
		/// <param name="payload">Send payload with user message.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task SendMessageViaLocalAIAsync(ChatSendRequestPayload payload)
		{
			// Step 1: Get system prompt and history from server
			var preparePayload = new { message = payload.Message ?? "" };
			var prepareJson = await HttpClient.PostJsonTaskAsync(NetworkEndpoints.ChatPrepareLocal, preparePayload);
			if (string.IsNullOrWhiteSpace(prepareJson))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Failed to prepare local AI prompt from server."
				});
				return;
			}

			var prepareResponse = JsonConvert.DeserializeObject<ChatPrepareLocalResponsePayload>(prepareJson);
			if (prepareResponse == null || string.IsNullOrWhiteSpace(prepareResponse.SystemPrompt))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Server returned invalid preparation data for local AI."
				});
				return;
			}

			// Step 1.5: Load full history for Ollama analysis (includes system/developer messages)
			var fullHistory = await LoadOllamaFullHistoryAsync();
			if (fullHistory == null)
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Failed to load full history for local Ollama analysis."
				});
				return;
			}

			// Step 2: Generate reply via local Ollama
			var ollamaResponse = await OllamaService.SendChatAsync(
				prepareResponse.SystemPrompt,
				fullHistory,
				payload.Message
			);

			if (ollamaResponse?.Message == null || string.IsNullOrWhiteSpace(ollamaResponse.Message.Content))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Local AI (Ollama) did not return a reply. Make sure Ollama is running."
				});
				return;
			}

			var rawReply = ollamaResponse.Message.Content;
			var jsonReply = await EnsureValidOllamaReplyJsonAsync(rawReply, prepareResponse.SystemPrompt);
			if (string.IsNullOrWhiteSpace(jsonReply))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Local AI returned invalid JSON reply format and could not repair it. History was not saved."
				});
				return;
			}

			var normalizedMessageIdReply = ReplaceAssistantMessageIdsWithSystemGuids(jsonReply);
			if (string.IsNullOrWhiteSpace(normalizedMessageIdReply))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Failed to normalize assistant MessageId before saving history."
				});
				return;
			}

			// Step 3: Save to server history
			var savePayload = new ChatSaveLocalRequestPayload
			{
				Message = payload.Message ?? "",
				Reply = normalizedMessageIdReply,
			};

			var saveJson = await HttpClient.PostJsonTaskAsync(NetworkEndpoints.ChatSaveLocal, savePayload);
			var saveResponse = string.IsNullOrWhiteSpace(saveJson)
				? null
				: JsonConvert.DeserializeObject<ChatSaveLocalResponsePayload>(saveJson);

			// Use cleaned reply from server if available (memory sidecars stripped)
			var effectiveReply = saveResponse != null && !string.IsNullOrWhiteSpace(saveResponse.Reply)
				? saveResponse.Reply
				: normalizedMessageIdReply;

			// Step 4: Publish to views
			var turns = ParseAssistantTurns(effectiveReply);

			EventBus.Publish(ChatEvents.MessageReceived, new ChatAssistantMessagePayload
			{
				Reply = effectiveReply,
				Model = "ollama/" + OllamaService.DefaultModel,
				SessionId = payload.SessionId,
				Turns = turns,
			});

			_ = PreResolveTtsAudioUrlsAsync(turns);
		}

		/// <summary>
		/// Generates a reply from history using local Ollama AI on desktop platforms.
		/// Similar to SendMessageViaLocalAIAsync but without a new user message.
		/// </summary>
		/// <param name="payload">Optional payload for session context.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task GenerateReplyFromHistoryViaLocalAIAsync(ChatSendRequestPayload payload)
		{
			// Step 1: Get system prompt and history from server
			var preparePayload = new { message = "" };
			var prepareJson = await HttpClient.PostJsonTaskAsync(NetworkEndpoints.ChatPrepareLocal, preparePayload);
			if (string.IsNullOrWhiteSpace(prepareJson))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Failed to prepare local AI prompt from server."
				});
				return;
			}

			var prepareResponse = JsonConvert.DeserializeObject<ChatPrepareLocalResponsePayload>(prepareJson);
			if (prepareResponse == null || string.IsNullOrWhiteSpace(prepareResponse.SystemPrompt))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Server returned invalid preparation data for local AI."
				});
				return;
			}

			// Step 1.5: Load full history for Ollama analysis (includes system/developer messages)
			var fullHistory = await LoadOllamaFullHistoryAsync();
			if (fullHistory == null)
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Failed to load full history for local Ollama analysis."
				});
				return;
			}

			// Step 2: Generate reply via local Ollama (no user message, just continue from history)
			var continuePrompt = "Continue the conversation naturally based on the current context.";
			var ollamaResponse = await OllamaService.SendChatAsync(
				prepareResponse.SystemPrompt,
				fullHistory,
				continuePrompt
			);

			if (ollamaResponse?.Message == null || string.IsNullOrWhiteSpace(ollamaResponse.Message.Content))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Local AI (Ollama) did not return a reply. Make sure Ollama is running."
				});
				return;
			}

			var rawReply = ollamaResponse.Message.Content;
			var jsonReply = await EnsureValidOllamaReplyJsonAsync(rawReply, prepareResponse.SystemPrompt);
			if (string.IsNullOrWhiteSpace(jsonReply))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Local AI returned invalid JSON reply format and could not repair it. History was not saved."
				});
				return;
			}

			var normalizedMessageIdReply = ReplaceAssistantMessageIdsWithSystemGuids(jsonReply);
			if (string.IsNullOrWhiteSpace(normalizedMessageIdReply))
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Failed to normalize assistant MessageId before saving history."
				});
				return;
			}

			// Step 3: Save to server history (no user message for respond-from-history)
			var savePayload = new ChatSaveLocalRequestPayload
			{
				Message = "",
				Reply = normalizedMessageIdReply,
			};

			var saveJson = await HttpClient.PostJsonTaskAsync(NetworkEndpoints.ChatSaveLocal, savePayload);
			var saveResponse = string.IsNullOrWhiteSpace(saveJson)
				? null
				: JsonConvert.DeserializeObject<ChatSaveLocalResponsePayload>(saveJson);

			var effectiveReply = saveResponse != null && !string.IsNullOrWhiteSpace(saveResponse.Reply)
				? saveResponse.Reply
				: normalizedMessageIdReply;

			// Step 4: Publish to views
			var turns = ParseAssistantTurns(effectiveReply);

			EventBus.Publish(ChatEvents.MessageReceived, new ChatAssistantMessagePayload
			{
				Reply = effectiveReply,
				Model = "ollama/" + OllamaService.DefaultModel,
				SessionId = payload?.SessionId,
				Turns = turns,
			});

			_ = PreResolveTtsAudioUrlsAsync(turns);
		}

		/// <summary>
		/// Ensures a local Ollama reply is valid assistant-turn JSON.
		/// If invalid, normalizes malformed wrappers/special characters and requests Ollama
		/// to reformat into the required JSON shape.
		/// Returns null when repair fails.
		/// </summary>
		/// <param name="reply">Raw Ollama assistant reply.</param>
		/// <param name="systemPrompt">Original chat system prompt used for generation.</param>
		/// <returns>Valid assistant-turn JSON, or null when unrecoverable.</returns>
		private static async Task<string> EnsureValidOllamaReplyJsonAsync(string reply, string systemPrompt)
		{
			var current = NormalizePotentialOllamaJsonReply(reply);
			if (IsValidAssistantReplyJson(current))
			{
				return current;
			}

			const int maxRepairAttempts = 2;
			for (var attempt = 1; attempt <= maxRepairAttempts; attempt++)
			{
				Debug.Log("[ChatController] Ollama reply JSON invalid. Requesting repair attempt " + attempt + ".");

				var repaired = await OllamaService.RepairReplyJsonAsync(current, systemPrompt);
				var repairedContent = NormalizePotentialOllamaJsonReply(repaired?.Message?.Content);
				if (string.IsNullOrWhiteSpace(repairedContent))
				{
					break;
				}

				if (IsValidAssistantReplyJson(repairedContent))
				{
					Debug.Log("[ChatController] Ollama reply JSON repaired successfully.");
					return repairedContent;
				}

				current = repairedContent;
			}

			return null;
		}

		/// <summary>
		/// Normalizes candidate JSON text from Ollama by removing markdown code fences,
		/// stripping invisible/control characters, and trimming surrounding noise.
		/// </summary>
		/// <param name="reply">Raw assistant reply text.</param>
		/// <returns>Sanitized text that is easier to parse as JSON.</returns>
		private static string NormalizePotentialOllamaJsonReply(string reply)
		{
			if (string.IsNullOrWhiteSpace(reply))
			{
				return "";
			}

			var normalized = StripMarkdownJsonFence(reply.Trim());

			var sanitizedBuilder = new StringBuilder(normalized.Length);
			for (var i = 0; i < normalized.Length; i++)
			{
				var ch = normalized[i];

				if (ch == '\uFEFF' || ch == '\u200B' || ch == '\u200C' || ch == '\u200D' || ch == '\u2060')
				{
					continue;
				}

				if (char.IsControl(ch) && ch != '\r' && ch != '\n' && ch != '\t')
				{
					continue;
				}

				sanitizedBuilder.Append(ch);
			}

			normalized = sanitizedBuilder.ToString().Trim();

			var firstObjectIndex = normalized.IndexOf('{');
			var firstArrayIndex = normalized.IndexOf('[');

			var startIndex = -1;
			if (firstObjectIndex >= 0 && firstArrayIndex >= 0)
			{
				startIndex = Math.Min(firstObjectIndex, firstArrayIndex);
			}
			else
			{
				startIndex = Math.Max(firstObjectIndex, firstArrayIndex);
			}

			var lastObjectIndex = normalized.LastIndexOf('}');
			var lastArrayIndex = normalized.LastIndexOf(']');
			var endIndex = Math.Max(lastObjectIndex, lastArrayIndex);

			if (startIndex >= 0 && endIndex >= startIndex)
			{
				normalized = normalized.Substring(startIndex, endIndex - startIndex + 1).Trim();
			}

			return normalized;
		}

		/// <summary>
		/// Removes a surrounding markdown fence block like ```json ... ```.
		/// </summary>
		/// <param name="content">Potential fenced content.</param>
		/// <returns>Inner content when fenced; otherwise original trimmed content.</returns>
		private static string StripMarkdownJsonFence(string content)
		{
			if (string.IsNullOrWhiteSpace(content))
			{
				return "";
			}

			var trimmed = content.Trim();
			if (!trimmed.StartsWith("```", StringComparison.Ordinal))
			{
				return trimmed;
			}

			var firstLineEndIndex = trimmed.IndexOf('\n');
			if (firstLineEndIndex < 0)
			{
				return trimmed.Replace("```", "").Trim();
			}

			var contentStartIndex = firstLineEndIndex + 1;
			var lastFenceIndex = trimmed.LastIndexOf("```", StringComparison.Ordinal);
			if (lastFenceIndex > contentStartIndex)
			{
				return trimmed.Substring(contentStartIndex, lastFenceIndex - contentStartIndex).Trim();
			}

			return trimmed.Substring(contentStartIndex).Trim();
		}

		/// <summary>
		/// Loads full unfiltered chat history from the dedicated local endpoint for Ollama.
		/// Returns null when request/parse fails.
		/// </summary>
		/// <returns>Full history list, or null on failure.</returns>
		private static async Task<List<ChatHistoryMessagePayload>> LoadOllamaFullHistoryAsync()
		{
			var historyJson = await HttpClient.GetTaskAsync(NetworkEndpoints.ChatHistoryLocal);
			if (string.IsNullOrWhiteSpace(historyJson))
			{
				return null;
			}

			try
			{
				var response = JsonConvert.DeserializeObject<ChatHistoryResponsePayload>(historyJson);
				return response?.Messages ?? new List<ChatHistoryMessagePayload>();
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// Validates whether a reply can be parsed as assistant-turn JSON.
		/// Requires at least one turn and full required prompt fields.
		/// </summary>
		/// <param name="reply">Reply text to validate.</param>
		/// <returns>True when JSON structure is valid for chat turns.</returns>
		private static bool IsValidAssistantReplyJson(string reply)
		{
			if (string.IsNullOrWhiteSpace(reply))
			{
				return false;
			}

			try
			{
				var parsedList = ParseAssistantTurns(reply);
				if (parsedList != null && parsedList.Count > 0)
				{
					for (var i = 0; i < parsedList.Count; i++)
					{
						if (!IsValidAssistantTurn(parsedList[i]))
						{
							return false;
						}
					}
					return true;
				}
			}
			catch
			{
			}

			return false;
		}

		/// <summary>
		/// Validates one assistant turn against required prompt fields.
		/// </summary>
		/// <param name="turn">Assistant turn payload.</param>
		/// <returns>True when all required fields are present.</returns>
		private static bool IsValidAssistantTurn(ChatAssistantTurnPayload turn)
		{
			return turn != null
				&& !string.IsNullOrWhiteSpace(turn.MessageId)
				&& !string.IsNullOrWhiteSpace(turn.CharacterName)
				&& !string.IsNullOrWhiteSpace(turn.Text)
				&& !string.IsNullOrWhiteSpace(turn.Pinyin)
				&& !string.IsNullOrWhiteSpace(turn.Tone)
				&& !string.IsNullOrWhiteSpace(turn.Translation);
		}

		/// <summary>
		/// Replaces all assistant MessageId values with system-generated GUIDs.
		/// Preserves all other fields (including optional memory sidecar fields).
		/// </summary>
		/// <param name="replyJson">Validated assistant reply JSON (array or single object).</param>
		/// <returns>JSON with new MessageId values, or null when parsing fails.</returns>
		private static string ReplaceAssistantMessageIdsWithSystemGuids(string replyJson)
		{
			if (string.IsNullOrWhiteSpace(replyJson))
			{
				return null;
			}

			var trimmed = replyJson.Trim();

			// 1) Handle Pipe-Delimited
			if (!trimmed.StartsWith("[") && !trimmed.StartsWith("{") && trimmed.Split('|').Length >= 7)
			{
				var lines = trimmed.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
				var builder = new StringBuilder();
				var replacedAny = false;
				for (var index = 0; index < lines.Length; index++)
				{
					var line = lines[index];
					var parts = line.Split('|');
					if (parts.Length >= 7)
					{
						parts[0] = Guid.NewGuid().ToString();
						builder.AppendLine(string.Join("|", parts));
						replacedAny = true;
					}
					else
					{
						builder.AppendLine(line);
					}
				}
				
				if (replacedAny)
				{
					return builder.ToString().Trim();
				}
			}

			// 2) Handle JSON Fallback
			try
			{
				var token = JToken.Parse(replyJson);

				if (token is JArray array)
				{
					for (var i = 0; i < array.Count; i++)
					{
						if (!(array[i] is JObject obj))
						{
							return null;
						}

						obj["MessageId"] = Guid.NewGuid().ToString();
					}

					return array.ToString(Formatting.None);
				}

				if (token is JObject single)
				{
					single["MessageId"] = Guid.NewGuid().ToString();
					return single.ToString(Formatting.None);
				}
			}
			catch
			{
			}

			return null;
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

			var trimmed = reply.Trim();

			// 1) Try Pipe-Delimited format first
			if (!trimmed.StartsWith("[") && !trimmed.StartsWith("{") && trimmed.Split('|').Length >= 7)
			{
				var lines = trimmed.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
				var turns = new List<ChatAssistantTurnPayload>();
				foreach (var line in lines)
				{
					var parts = line.Split('|');
					if (parts.Length >= 7)
					{
						var characterName = parts[1].Trim();
						var emotion = parts[4].Trim().ToLower();
						var intensity = parts[5].Trim().ToLower();
						var tone = $"{emotion}, {intensity}";
						var pinyin = parts[3].Trim();
						if (characterName == "\u53d9\u8ff0\u8005" && pinyin == "-")
						{
							pinyin = "";
						}

						turns.Add(new ChatAssistantTurnPayload
						{
							MessageId = parts[0].Trim(),
							CharacterName = characterName,
							Text = parts[2].Trim(),
							Pinyin = pinyin,
							Tone = tone,
							Emotion = emotion,
							Intensity = intensity,
							Translation = parts[6].Trim()
						});
					}
				}

				if (turns.Count > 0)
				{
					return turns;
				}
			}

			// 2) Fallback to JSON
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
		/// Pre-resolves TTS audio URLs for each turn so the View only needs to download audio clips.
		/// When the character uses gpt-sovits, the full local TTS pipeline is executed.
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
					turn.AudioUrl = await ResolveTtsAudioUrlAsync(
						turn.Text, tone, characterName, false, turn.MessageId,
						turn.Emotion, turn.Intensity);

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
		/// Resolves audio for a single turn. Called sequentially by the View.
		/// </summary>
		private static async Task ResolveTurnAudioInternalAsync(ChatAssistantTurnPayload turn)
		{
			if (turn == null || turn.IsAudioPreloadCompleted) return;

			try
			{
				if (string.IsNullOrWhiteSpace(turn.Text))
				{
					turn.IsAudioPreloadCompleted = true;
					return;
				}

				var characterName = string.IsNullOrWhiteSpace(turn.CharacterName) ? "Mimi" : turn.CharacterName.Trim();
				var tone = string.IsNullOrWhiteSpace(turn.Tone) ? "neutral" : turn.Tone.Trim();

				turn.AudioUrl = await ResolveTtsAudioUrlAsync(
					turn.Text, tone, characterName, false, turn.MessageId,
					turn.Emotion, turn.Intensity);

				if (!string.IsNullOrWhiteSpace(turn.AudioUrl))
				{
					var audioType = AudioUrlUtils.ResolveAudioType(turn.AudioUrl);
					turn.AudioClip = await HttpClient.DownloadAudioClipTaskAsync(turn.AudioUrl, audioType);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[ChatController] Failed to resolve turn audio: " + ex.Message);
			}
			finally
			{
				turn.IsAudioPreloadCompleted = true;
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

				var audioUrl = await ResolveTtsAudioUrlAsync(payload.Text, payload.Tone, characterName, payload.ForceReload, payload.MessageId, payload.Emotion, payload.Intensity);

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
					Emotion = payload.Emotion,
					Intensity = payload.Intensity,
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
		/// Generates a story-relevant example sentence for a vocabulary word via API.
		/// </summary>
		/// <param name="payload">Request payload with word.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task GenerateVocabExampleInternalAsync(ChatVocabExampleRequestPayload payload)
		{
			try
			{
				var word = payload.Word.Trim();
				var body = new ChatVocabExampleRequestPayload { Word = word };
				var responseJson = await HttpClient.PostJsonTaskAsync(NetworkEndpoints.ChatGenerateVocabExample, body);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
					{
						Message = "Empty response from example sentence generation."
					});
					return;
				}

				var response = JsonConvert.DeserializeObject<ChatVocabExampleResponsePayload>(responseJson);
				if (response == null || string.IsNullOrWhiteSpace(response.Sentence))
				{
					EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
					{
						Message = "Failed to parse example sentence response."
					});
					return;
				}

				EventBus.Publish(ChatEvents.VocabExampleGenerated, new ChatVocabExampleResultPayload
				{
					Word = word,
					Sentence = response.Sentence,
					Pinyin = response.Pinyin,
					Translation = response.Translation
				});
			}
			catch (Exception exception)
			{
				EventBus.Publish(ChatEvents.RequestFailed, new ChatErrorPayload
				{
					Message = "Failed to generate example sentence: " + exception.Message
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
		/// Loads due vocabulary from the review endpoint and new vocabulary from
		/// learning paths, then publishes the combined result. Each API call is
		/// independent so a failure in one does not block the other.
		/// </summary>
		/// <returns>Awaitable task.</returns>
		private static async Task LoadAutoChatVocabularyInternalAsync()
		{
			try
			{
				// Fetch due vocabulary, learning paths, and today's new count independently.
				string dueJson = null;
				string learningPathsJson = null;
				string todayNewCountJson = null;

				try
				{
					dueJson = await HttpClient.GetTaskAsync(NetworkEndpoints.VocabularyDue);
				}
				catch (Exception dueException)
				{
					Debug.LogWarning("[ChatController] Failed to load due vocabulary: " + dueException.Message);
				}

				try
				{
					learningPathsJson = await HttpClient.GetTaskAsync(NetworkEndpoints.LearningPaths);
				}
				catch (Exception lpException)
				{
					Debug.LogWarning("[ChatController] Failed to load learning paths: " + lpException.Message);
				}

				try
				{
					todayNewCountJson = await HttpClient.GetTaskAsync(NetworkEndpoints.VocabularyTodayNewCount);
				}
				catch (Exception cntException)
				{
					Debug.LogWarning("[ChatController] Failed to load today's new vocab count: " + cntException.Message);
				}

				// Parse due vocabulary.
				var dueResponse = string.IsNullOrWhiteSpace(dueJson)
					? null
					: JsonConvert.DeserializeObject<ChatVocabularyListResponsePayload>(dueJson);

				var dueWords = new List<string>();
				if (dueResponse?.Vocabularies != null)
				{
					for (var i = 0; i < dueResponse.Vocabularies.Count; i++)
					{
						var word = dueResponse.Vocabularies[i]?.Korean;
						if (!string.IsNullOrWhiteSpace(word))
						{
							dueWords.Add(word.Trim());
						}
					}
				}

				// Parse new words from learning paths (comma-separated vocabulary field).
				var dueWordSet = new HashSet<string>(dueWords, StringComparer.OrdinalIgnoreCase);
				var newWords = new List<string>();
				var lpRoot = string.IsNullOrWhiteSpace(learningPathsJson)
					? null
					: JsonConvert.DeserializeObject<JObject>(learningPathsJson);
				var learningPaths = lpRoot?["learningPaths"] as JArray;

				if (learningPaths != null)
				{
					for (var i = 0; i < learningPaths.Count; i++)
					{
						var vocabCsv = learningPaths[i]?["vocabulary"]?.ToString();
						if (string.IsNullOrWhiteSpace(vocabCsv))
						{
							continue;
						}

						var words = vocabCsv.Split(',');
						for (var j = 0; j < words.Length; j++)
						{
							var word = words[j]?.Trim();
							if (!string.IsNullOrWhiteSpace(word) && !dueWordSet.Contains(word))
							{
								newWords.Add(word);
								dueWordSet.Add(word); // Prevent duplicates across learning paths.
							}
						}
					}
				}

				EventBus.Publish(ChatEvents.AutoChatVocabularyLoaded, new ChatAutoChatVocabularyPayload
				{
					DueWords = dueWords,
					NewWords = newWords,
					TodayNewCount = ParseTodayNewCount(todayNewCountJson),
				});
			}
			catch (Exception exception)
			{
				EventBus.Publish(ChatEvents.AutoChatVocabularyLoaded, new ChatAutoChatVocabularyPayload());
				Debug.LogWarning("[ChatController] Failed to load auto-chat vocabulary: " + exception.Message);
			}
		}

		/// <summary>
		/// Parses the today-new-count response payload, returning 0 on missing/invalid input.
		/// </summary>
		/// <param name="json">Raw JSON response body.</param>
		/// <returns>Count of vocabulary entries created today.</returns>
		private static int ParseTodayNewCount(string json)
		{
			if (string.IsNullOrWhiteSpace(json))
			{
				return 0;
			}

			try
			{
				var root = JsonConvert.DeserializeObject<JObject>(json);
				var token = root?["count"];
				if (token != null && token.Type != JTokenType.Null)
				{
					return token.Value<int>();
				}
			}
			catch (Exception parseException)
			{
				Debug.LogWarning("[ChatController] Failed to parse today's new vocab count: " + parseException.Message);
			}

			return 0;
		}

		/// <summary>
		/// Posts the list of used vocabulary words to the server for batch review.
		/// Fire-and-forget; failures are logged but do not block the user.
		/// </summary>
		/// <param name="payload">Batch review request with word list.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task BatchReviewAutoChatVocabularyInternalAsync(ChatBatchReviewVocabRequestPayload payload)
		{
			try
			{
				var json = JsonConvert.SerializeObject(payload);
				await HttpClient.PostJsonTaskAsync(NetworkEndpoints.VocabularyBatchReview, json);
				Debug.Log("[ChatController] Batch-reviewed " + payload.Words.Count + " auto-chat vocab words.");
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[ChatController] Failed to batch-review auto-chat vocabulary: " + exception.Message);
			}
		}

		/// <summary>
		/// Calls the TTS endpoint and returns the resolved absolute audio URL.
		/// On PC when character uses gpt-sovits voice model, uses the local GPT-SoVITS pipeline instead.
		/// </summary>
		/// <param name="text">Text to synthesize.</param>
		/// <param name="tone">Tone hint.</param>
		/// <param name="characterName">Character name.</param>
		/// <param name="forceReload">Whether to force regeneration.</param>
		/// <param name="messageId">Optional AI-generated message id for server-side rewrite tracking.</param>
		/// <param name="emotion">Optional emotion label from AI (used by GPT-SoVITS path).</param>
		/// <param name="intensity">Optional intensity label from AI (used by GPT-SoVITS path).</param>
		/// <returns>Absolute audio URL, or null on failure.</returns>
		private static async Task<string> ResolveTtsAudioUrlAsync(
			string text,
			string tone,
			string characterName,
			bool forceReload = false,
			string messageId = null,
			string emotion = null,
			string intensity = null)
		{
			try
			{
				// ── 1. Check if audio already exists on server (Cache lookup) ──────────
				if (!forceReload && !string.IsNullOrWhiteSpace(characterName))
				{
					var query = AudioUrlUtils.BuildTextToSpeechQuery(text, tone, characterName, forceReload, messageId);
					var endpoint = NetworkEndpoints.CheckAudio + query;
					var responseJson = await HttpClient.GetTaskAsync(endpoint);
					if (!string.IsNullOrWhiteSpace(responseJson))
					{
						var ttsResponse = JsonConvert.DeserializeObject<ChatCheckAudioResponsePayload>(responseJson);
						if (ttsResponse != null && ttsResponse.Exists == true && !string.IsNullOrWhiteSpace(ttsResponse.Url))
						{
							var settingsForUrl = UnityEngine.Resources.Load<NetworkSettings>("NetworkSettings");
							var baseUrl = AudioUrlUtils.NormalizeServerBaseUrl(settingsForUrl != null ? settingsForUrl.BaseUrl : null);
							return AudioUrlUtils.ResolveAudioUrl(ttsResponse.Url, baseUrl);
						}
					}
				}

				// ── 2. GPT-SoVITS path (PC only, when character uses gemini voice model) ──
				if (IsDesktopPlatform() && !string.IsNullOrWhiteSpace(characterName) && !string.Equals(characterName.Trim(), "User", StringComparison.OrdinalIgnoreCase))
				{
					var voiceModel = ChatState.ParentSignals?.GetCharacterVoiceModelByName?.Invoke(characterName.Trim());
					if (string.Equals(voiceModel, "gemini", StringComparison.OrdinalIgnoreCase))
					{
						var voiceName = ChatState.ParentSignals?.GetCharacterVoiceNameByName?.Invoke(characterName.Trim());
						var settings = UnityEngine.Resources.Load<NetworkSettings>("NetworkSettings");
						var serverBaseUrl = AudioUrlUtils.NormalizeServerBaseUrl(settings != null ? settings.BaseUrl : null);
						if (!string.IsNullOrWhiteSpace(serverBaseUrl))
						{
							var mp3Url = await Share.Utils.GptSoVitsTtsService.SynthesizeAsync(
								text,
								emotion,
								intensity,
								voiceName,
								tone,
								characterName,
								messageId,
								serverBaseUrl);

							if (!string.IsNullOrWhiteSpace(mp3Url))
							{
								return mp3Url;
							}

							Debug.LogWarning("[ChatController] GPT-SoVITS pipeline failed; falling back to server TTS.");
						}
					}
				}

				// ── 3. Standard server TTS path ─────────────────────────────────────────
				{
					var query = AudioUrlUtils.BuildTextToSpeechQuery(text, tone, characterName, forceReload, messageId);
					var endpoint = NetworkEndpoints.TextToSpeech + query;
					var responseJson = await HttpClient.GetTaskAsync(endpoint);
					if (string.IsNullOrWhiteSpace(responseJson))
					{
						return null;
					}

					var ttsResponse = JsonConvert.DeserializeObject<ChatTextToSpeechResponsePayload>(responseJson);

					// When the server rewrites the text for TTS compatibility, notify the view
					// so it can update the displayed message content.
					if (ttsResponse?.Rewritten == true && !string.IsNullOrWhiteSpace(messageId))
					{
						EventBus.Publish(ChatEvents.MessageContentUpdated, new ChatMessageContentUpdatedPayload
						{
							MessageId = messageId,
							Text = ttsResponse.Text,
							Pinyin = ttsResponse.Pinyin,
						});
					}

					var rawUrl = ttsResponse?.Url;
					if (string.IsNullOrWhiteSpace(rawUrl))
					{
						return null;
					}

					var settingsForUrl = UnityEngine.Resources.Load<NetworkSettings>("NetworkSettings");
					var baseUrl = AudioUrlUtils.NormalizeServerBaseUrl(settingsForUrl != null ? settingsForUrl.BaseUrl : null);
					return AudioUrlUtils.ResolveAudioUrl(rawUrl, baseUrl);
				}

			}
			catch (Exception exception)
			{
				Debug.LogWarning("[ChatController] TTS resolution failed: " + exception.Message);
				return null;
			}
		}
	}
}
