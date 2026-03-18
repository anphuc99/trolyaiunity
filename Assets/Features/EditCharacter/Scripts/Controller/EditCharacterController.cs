using System.Threading.Tasks;
using System.Collections.Generic;
using Core.Infrastructure.Network;
using Core.Infrastructure.Scenes;
using Core.Infrastructure.State;
using Features.EditCharacter.Events;
using Features.EditCharacter.Infrastructure;
using Features.EditCharacter.Infrastructure.Attributes;
using Features.EditCharacter.Model;
using Features.EditCharacter.Requests;
using Newtonsoft.Json;
using Share.Model;
using UnityEngine;
using UnityEngine.Networking;

namespace Features.EditCharacter.Controller
{
	/// <summary>
	/// Controller for EditCharacter.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.EditCharacterGameplay)]
	public static class EditCharacterController
	{
		private const string SelectedCharacterGlobalKey = "global.character.selected.info";
		private const string EditedCharacterGlobalKey = "global.character.edited.notice";

		private sealed class UpdateCharacterResponse
		{
			public int id;
			public string name;
			public string personality;
			public string gender;
			public int? age;
			public string avatar;
			public string voiceModel;
			public string voiceName;
			public float? pitch;
			public float? speakingRate;
		}

		private sealed class AvatarUploadResponse
		{
			public string url;
			public string message;
		}

		/// <summary>
		/// Called when the controller scope is entered.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerInit]
		public static void OnEnterScope()
		{
			PublishSelectedCharacter();
		}

		/// <summary>
		/// Called when the controller scope is exited.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerShutdown]
		public static void OnExitScope()
		{
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(EditCharacterRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(EditCharacterEvents.Echoed, payload);
		}

		[Request(EditCharacterRequests.OpenScope)]
		public static void HandleOpenScope()
		{
			LoadScene.ByScope(Core.Infrastructure.Attributes.ControllerScopeKey.EditCharacterGameplay, UnityEngine.SceneManagement.LoadSceneMode.Additive);
		}

		[Request(EditCharacterRequests.CloseScope)]
		public static void HandleCloseScope()
		{
			LoadScene.UnloadByScope(Core.Infrastructure.Attributes.ControllerScopeKey.EditCharacterGameplay);
		}

		[Request(EditCharacterRequests.LoadSelectedCharacter)]
		public static void HandleLoadSelectedCharacter()
		{
			PublishSelectedCharacter();
		}

		[Request(EditCharacterRequests.FetchVoices)]
		public static void HandleFetchVoices()
		{
			_ = FetchVoicesAsync();
		}

		[Request(EditCharacterRequests.UploadAvatar)]
		public static void HandleUploadAvatar(object payload)
		{
			if (payload is not EditCharacterAvatarUploadPayload uploadPayload || string.IsNullOrWhiteSpace(uploadPayload.image))
			{
				EventBus.Publish(EditCharacterEvents.AvatarUploadFailed, "Thiếu dữ liệu avatar.");
				return;
			}

			_ = UploadAvatarAsync(uploadPayload);
		}

		[Request(EditCharacterRequests.SubmitCharacter)]
		public static void HandleSubmitCharacter(object payload)
		{
			if (payload is not EditCharacterPayload editPayload)
			{
				EventBus.Publish(EditCharacterEvents.SubmitFailed, "Dữ liệu chỉnh sửa không hợp lệ.");
				return;
			}

			if (editPayload.id <= 0 || string.IsNullOrWhiteSpace(editPayload.name) || string.IsNullOrWhiteSpace(editPayload.gender))
			{
				EventBus.Publish(EditCharacterEvents.SubmitFailed, "Thông tin nhân vật không hợp lệ.");
				return;
			}

			_ = SubmitCharacterAsync(editPayload);
		}

		private static void PublishSelectedCharacter()
		{
			GlobalVariables.TryGet<SelectedCharacterInfo>(SelectedCharacterGlobalKey, out var selectedCharacter);
			EventBus.Publish(EditCharacterEvents.SelectedCharacterLoaded, selectedCharacter);
		}

		private static async Task UploadAvatarAsync(EditCharacterAvatarUploadPayload payload)
		{
			var result = await HttpClient.PostJsonTaskAsync(NetworkEndpoints.CharactersUploadAvatar, payload);
			if (string.IsNullOrWhiteSpace(result))
			{
				EventBus.Publish(EditCharacterEvents.AvatarUploadFailed, "Server không phản hồi khi upload avatar.");
				return;
			}

			try
			{
				var response = JsonConvert.DeserializeObject<AvatarUploadResponse>(result);
				if (response == null || string.IsNullOrWhiteSpace(response.url))
				{
					EventBus.Publish(EditCharacterEvents.AvatarUploadFailed, response?.message ?? "Upload avatar thất bại.");
					return;
				}

				var loadedSprite = await LoadSpriteFromUrlAsync(response.url);
				UpdateSelectedCharacterAvatar(response.url, loadedSprite);

				EventBus.Publish(EditCharacterEvents.AvatarUploadSucceeded, response.url);
			}
			catch
			{
				EventBus.Publish(EditCharacterEvents.AvatarUploadFailed, "Upload avatar thất bại.");
			}
		}

		private static async Task SubmitCharacterAsync(EditCharacterPayload payload)
		{
			GlobalVariables.TryGet<SelectedCharacterInfo>(SelectedCharacterGlobalKey, out var currentSelectedCharacter);
			var preservedAvatarSprite = currentSelectedCharacter?.Avatar;

			var request = new EditCharacterPayload
			{
				name = payload.name,
				personality = payload.personality,
				gender = payload.gender,
				age = payload.age,
				appearance = null,
				avatar = payload.avatar,
				voiceModel = NormalizeVoiceModel(payload.voiceModel, payload.voiceName),
				voiceName = payload.voiceName,
				pitch = payload.pitch,
				speakingRate = payload.speakingRate,
			};

			var endpoint = $"{NetworkEndpoints.Characters}/{payload.id}";
			var result = await HttpClient.PutJsonTaskAsync(endpoint, request);
			if (string.IsNullOrWhiteSpace(result))
			{
				EventBus.Publish(EditCharacterEvents.SubmitFailed, "Server không phản hồi khi cập nhật nhân vật.");
				return;
			}

			if (result.Contains("\"message\""))
			{
				EventBus.Publish(EditCharacterEvents.SubmitFailed, "Cập nhật nhân vật thất bại.");
				return;
			}

			try
			{
				var response = JsonConvert.DeserializeObject<UpdateCharacterResponse>(result);
				if (response == null || response.id <= 0)
				{
					response = new UpdateCharacterResponse
					{
						id = payload.id,
						name = payload.name,
						personality = payload.personality,
						gender = payload.gender,
						age = payload.age,
						avatar = payload.avatar,
						voiceModel = payload.voiceModel,
						voiceName = payload.voiceName,
						pitch = payload.pitch,
						speakingRate = payload.speakingRate,
					};
				}

				var updated = new SelectedCharacterInfo
				{
					Id = response.id,
					Name = response.name,
					Age = response.age,
					Description = response.personality,
					Gender = response.gender,
					VoiceModel = response.voiceModel,
					VoiceName = response.voiceName,
					Pitch = response.pitch,
					SpeakingRate = response.speakingRate,
					AvatarUrl = response.avatar,
					Avatar = preservedAvatarSprite,
				};

				SetEditedCharacterNotice(updated, response.avatar, response.speakingRate);
				EventBus.Publish(EditCharacterEvents.SubmitSucceeded, updated);
				LoadScene.UnloadByScope(Core.Infrastructure.Attributes.ControllerScopeKey.EditCharacterGameplay);
			}
			catch
			{
				EventBus.Publish(EditCharacterEvents.SubmitFailed, "Cập nhật nhân vật thất bại.");
			}
		}

		private static async Task FetchVoicesAsync()
		{
			var json = await HttpClient.GetTaskAsync(NetworkEndpoints.Voices);
			if (string.IsNullOrWhiteSpace(json))
			{
				EventBus.Publish(EditCharacterEvents.VoicesLoaded, null);
				return;
			}

			try
			{
				var data = JsonConvert.DeserializeObject<EditVoiceOptionsResponse>(json);
				EditCharacterModel.AvailableVoices = data?.voices ?? new List<EditVoiceOptionData>();
				EventBus.Publish(EditCharacterEvents.VoicesLoaded, EditCharacterModel.AvailableVoices);
			}
			catch
			{
				EventBus.Publish(EditCharacterEvents.VoicesLoaded, null);
			}
		}

		private static string NormalizeVoiceModel(string voiceModel, string voiceName)
		{
			if (string.IsNullOrWhiteSpace(voiceName))
			{
				return null;
			}

			var model = (voiceModel ?? string.Empty).Trim().ToLowerInvariant();
			if (model == "openai" || model == "gemini")
			{
				return model;
			}

			return "openai";
		}

		private static void SetEditedCharacterNotice(SelectedCharacterInfo updated, string avatarUrl, float? speakingRate)
		{
			GlobalVariables.Set(SelectedCharacterGlobalKey, updated);
			GlobalVariables.Set(EditedCharacterGlobalKey, new EditedCharacterNotice
			{
				Character = updated,
				AvatarUrl = avatarUrl,
				SpeakingRate = speakingRate,
			});
		}

		private static void UpdateSelectedCharacterAvatar(string avatarUrl, Sprite avatarSprite)
		{
			if (!GlobalVariables.TryGet<SelectedCharacterInfo>(SelectedCharacterGlobalKey, out var current)
				|| current == null)
			{
				return;
			}

			current.AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? current.AvatarUrl : avatarUrl;
			if (avatarSprite != null)
			{
				current.Avatar = avatarSprite;
			}

			GlobalVariables.Set(SelectedCharacterGlobalKey, current);
		}

		private static async Task<Sprite> LoadSpriteFromUrlAsync(string avatarUrl)
		{
			if (string.IsNullOrWhiteSpace(avatarUrl))
			{
				return null;
			}

			using var request = UnityWebRequestTexture.GetTexture(avatarUrl);
			UnityWebRequestAsyncOperation operation;
			try
			{
				operation = request.SendWebRequest();
			}
			catch (System.Exception exception)
			{
				Debug.LogError("[EditCharacterController] Failed to start avatar request for URL '" + avatarUrl + "': " + exception);
				return null;
			}

			while (!operation.isDone)
			{
				await Task.Yield();
			}

			if (request.result != UnityWebRequest.Result.Success)
			{
				Debug.LogError("[EditCharacterController] Failed to load avatar sprite from URL '" + avatarUrl + "': " + request.error);
				return null;
			}

			var texture = DownloadHandlerTexture.GetContent(request);
			if (texture == null)
			{
				return null;
			}

			return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
		}
	}
}
