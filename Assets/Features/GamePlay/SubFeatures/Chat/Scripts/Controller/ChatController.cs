using Features.GamePlay.SubFeatures.Chat.Events;
using Features.GamePlay.SubFeatures.Chat.Infrastructure;
using Features.GamePlay.SubFeatures.Chat.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Chat.Model;
using Features.GamePlay.SubFeatures.Chat.Requests;
using Core.Infrastructure.Network;
using Newtonsoft.Json;
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
			EventBus.Publish(ChatEvents.Installed, null);
		}

		/// <summary>
		/// Uninstalls the subcontroller and clears bound parent signals.
		/// </summary>
		public static void Uninstall()
		{
			EventBus.Publish(ChatEvents.Uninstalled, null);
		}

		/// <summary>
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(ChatParentSignals signals)
		{
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
				var responseJson = await HttpClient.PostJsonTaskAsync(NetworkEndpoints.ChatSend, payload);
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
		/// Builds history endpoint with optional session query parameter.
		/// </summary>
		/// <param name="sessionId">Optional session id.</param>
		/// <returns>Resolved endpoint path.</returns>
		private static string BuildHistoryEndpoint(string sessionId)
		{
			if (string.IsNullOrWhiteSpace(sessionId))
			{
				return NetworkEndpoints.ChatHistory;
			}

			return NetworkEndpoints.ChatHistory + "?sessionId=" + Uri.EscapeDataString(sessionId.Trim());
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
