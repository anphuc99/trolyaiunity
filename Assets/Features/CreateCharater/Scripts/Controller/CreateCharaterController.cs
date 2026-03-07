using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Infrastructure.Network;
using Core.Infrastructure.Scenes;
using Features.CreateCharater.Events;
using Features.CreateCharater.Infrastructure;
using Features.CreateCharater.Infrastructure.Attributes;
using Features.CreateCharater.Model;
using Features.CreateCharater.Requests;
using Newtonsoft.Json;
using UnityEngine;

namespace Features.CreateCharater.Controller
{
	/// <summary>
	/// Controller for CreateCharater.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.CreateCharaterGameplay)]
	public static class CreateCharaterController
	{
		private sealed class CharacterCreateResponse
		{
			public int id;
			public string message;
			public string status;
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
		}

		/// <summary>
		/// Called when the controller scope is exited.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerShutdown]
		public static void OnExitScope()
		{
			CreateCharaterModel.AvailablePersonalities.Clear();
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(CreateCharaterRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(CreateCharaterEvents.Echoed, payload);
		}

		/// <summary>
		/// Fetches personalities from the server.
		/// </summary>
		[Request(CreateCharaterRequests.FetchPersonalities)]
		public static void FetchPersonalities()
		{
			_ = FetchPersonalitiesAsync();
		}

		private static async Task FetchPersonalitiesAsync()
		{
			var json = await HttpClient.GetTaskAsync(NetworkEndpoints.Personalities);
			if (string.IsNullOrWhiteSpace(json))
			{
				EventBus.Publish(CreateCharaterEvents.PersonalitiesLoaded, null);
				return;
			}

			try
			{
				var data = JsonConvert.DeserializeObject<List<PersonalityData>>(json);
				CreateCharaterModel.AvailablePersonalities = data ?? new List<PersonalityData>();
				EventBus.Publish(CreateCharaterEvents.PersonalitiesLoaded, CreateCharaterModel.AvailablePersonalities);
			}
			catch (System.Exception e)
			{
				Debug.LogError($"[CreateCharaterController] Failed to parse personalities: {e.Message}");
				EventBus.Publish(CreateCharaterEvents.PersonalitiesLoaded, null);
			}
		}

		/// <summary>
		/// Submits a new character to the server.
		/// </summary>
		/// <param name="payload">Character creation payload.</param>
		[Request(CreateCharaterRequests.SubmitCharacter)]
		public static void SubmitCharacter(CreateCharacterPayload payload)
		{
			if (payload == null)
			{
				EventBus.Publish(CreateCharaterEvents.CharacterCreationFailed, "Dữ liệu không hợp lệ.");
				return;
			}

			var normalizedPayload = NormalizePayload(payload);
			if (normalizedPayload == null)
			{
				EventBus.Publish(CreateCharaterEvents.CharacterCreationFailed, "Vui lòng nhập tên, mô tả và giới tính hợp lệ.");
				return;
			}

			_ = SubmitCharacterAsync(normalizedPayload);
		}

		/// <summary>
		/// Uploads avatar image to server and publishes resulting URL.
		/// </summary>
		/// <param name="payload">Avatar upload payload containing base64 data URL.</param>
		[Request(CreateCharaterRequests.UploadAvatar)]
		public static void UploadAvatar(AvatarUploadPayload payload)
		{
			if (payload == null || string.IsNullOrWhiteSpace(payload.image))
			{
				EventBus.Publish(CreateCharaterEvents.AvatarUploadFailed, "Thiếu dữ liệu ảnh avatar.");
				return;
			}

			_ = UploadAvatarAsync(payload);
		}

		/// <summary>
		/// Closes CreateCharater scope.
		/// </summary>
		[Request(CreateCharaterRequests.CloseScope)]
		public static void HandleCloseScope()
		{
			LoadScene.UnloadByScope(Core.Infrastructure.Attributes.ControllerScopeKey.CreateCharaterGameplay);
		}

		private static CreateCharacterPayload NormalizePayload(CreateCharacterPayload payload)
		{
			var name = (payload.name ?? string.Empty).Trim();
			var personality = (payload.personality ?? string.Empty).Trim();
			var genderRaw = (payload.gender ?? string.Empty).Trim().ToLowerInvariant();

			if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(personality))
			{
				return null;
			}

			var gender = string.Empty;
			if (genderRaw == "male" || genderRaw == "nam")
			{
				gender = "male";
			}
			else if (genderRaw == "female" || genderRaw == "nu" || genderRaw == "nữ")
			{
				gender = "female";
			}

			if (string.IsNullOrWhiteSpace(gender))
			{
				return null;
			}

			int? age = null;
			if (payload.age.HasValue)
			{
				var parsed = payload.age.Value;
				if (parsed < 0 || parsed > 150)
				{
					return null;
				}

				age = parsed;
			}

			var voiceName = string.IsNullOrWhiteSpace(payload.voiceName) ? null : payload.voiceName.Trim();

			return new CreateCharacterPayload
			{
				name = name,
				age = age,
				personality = personality,
				gender = gender,
				appearance = string.IsNullOrWhiteSpace(payload.appearance) ? null : payload.appearance.Trim(),
				avatar = string.IsNullOrWhiteSpace(payload.avatar) ? null : payload.avatar.Trim(),
				voiceModel = string.IsNullOrWhiteSpace(voiceName) ? null : "openai",
				voiceName = voiceName,
				pitch = payload.pitch,
				speakingRate = payload.speakingRate
			};
		}

		private static async Task SubmitCharacterAsync(CreateCharacterPayload payload)
		{
			var result = await HttpClient.PostJsonTaskAsync(NetworkEndpoints.Characters, payload);
			if (string.IsNullOrWhiteSpace(result))
			{
				EventBus.Publish(CreateCharaterEvents.CharacterCreationFailed, "Server không phản hồi.");
				return;
			}

			try
			{
				var response = JsonConvert.DeserializeObject<CharacterCreateResponse>(result);
				var isSuccess = response != null && (response.id > 0 || response.status == "success");

				if (!isSuccess)
				{
					var message = response != null && !string.IsNullOrWhiteSpace(response.message)
						? response.message
						: "Tạo nhân vật thất bại.";
					EventBus.Publish(CreateCharaterEvents.CharacterCreationFailed, message);
					return;
				}
			}
			catch (System.Exception)
			{
				if (!result.Contains("\"id\"") && !result.Contains("\"status\":\"success\""))
				{
					EventBus.Publish(CreateCharaterEvents.CharacterCreationFailed, "Tạo nhân vật thất bại.");
					return;
				}
			}

			EventBus.Publish(CreateCharaterEvents.CharacterCreationSucceeded);

			if (Application.isPlaying)
			{
				LoadScene.ByScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay);
			}
		}

		private static async Task UploadAvatarAsync(AvatarUploadPayload payload)
		{
			var result = await HttpClient.PostJsonTaskAsync(NetworkEndpoints.CharactersUploadAvatar, payload);
			if (string.IsNullOrWhiteSpace(result))
			{
				EventBus.Publish(CreateCharaterEvents.AvatarUploadFailed, "Server không phản hồi khi upload avatar.");
				return;
			}

			try
			{
				var response = JsonConvert.DeserializeObject<AvatarUploadResponse>(result);
				if (response == null || string.IsNullOrWhiteSpace(response.url))
				{
					var message = response != null && !string.IsNullOrWhiteSpace(response.message)
						? response.message
						: "Upload avatar thất bại.";
					EventBus.Publish(CreateCharaterEvents.AvatarUploadFailed, message);
					return;
				}

				EventBus.Publish(CreateCharaterEvents.AvatarUploadSucceeded, response.url);
			}
			catch (System.Exception)
			{
				EventBus.Publish(CreateCharaterEvents.AvatarUploadFailed, "Upload avatar thất bại.");
			}
		}
	}
}
