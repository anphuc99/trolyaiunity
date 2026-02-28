using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Infrastructure.Network;
using Features.GamePlay.SubFeatures.Setting.Events;
using Features.GamePlay.SubFeatures.Setting.Infrastructure;
using Features.GamePlay.SubFeatures.Setting.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Setting.Model;
using Features.GamePlay.SubFeatures.Setting.Requests;
using Newtonsoft.Json;

namespace Features.GamePlay.SubFeatures.Setting.Controller
{
	/// <summary>
	/// Controller for Setting.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class SettingController
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
			SettingState.Reset();
		}

		/// <summary>
		/// Installs the Setting subfeature.
		/// </summary>
		public static void Install()
		{
			EventBus.Publish(SettingEvents.Installed, null);
		}

		/// <summary>
		/// Uninstalls the Setting subfeature.
		/// </summary>
		public static void Uninstall()
		{
			EventBus.Publish(SettingEvents.Uninstalled, null);
		}

		/// <summary>
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(SettingParentSignals signals)
		{
			SettingState.ParentSignals = signals;
		}

		/// <summary>
		/// Loads the user's profile plus dropdown options.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[Request(SettingRequests.LoadProfile)]
		public static void HandleLoadProfile(object payload)
		{
			_ = LoadProfileInternalAsync();
		}

		/// <summary>
		/// Saves the user's profile via the API.
		/// </summary>
		/// <param name="payload">Profile payload supplied by the view.</param>
		[Request(SettingRequests.SaveProfile)]
		public static void HandleSaveProfile(SettingProfileSaveRequestPayload payload)
		{
			var validationError = ValidateSavePayload(payload);
			if (!string.IsNullOrEmpty(validationError))
			{
				PublishSaveFailed(validationError);
				return;
			}

			_ = SaveProfileInternalAsync(payload);
		}

		private static async Task LoadProfileInternalAsync()
		{
			EventBus.Publish(SettingEvents.ProfileLoadStarted, null);

			try
			{
				var userTask = HttpClient.GetTaskAsync(NetworkEndpoints.UserMe);
				var levelsTask = HttpClient.GetTaskAsync(NetworkEndpoints.Levels);
				var storiesTask = HttpClient.GetTaskAsync(NetworkEndpoints.Stories);

				await Task.WhenAll(userTask, levelsTask, storiesTask);

				var userResponse = ParseResponse<SettingUserResponsePayload>(await userTask);
				if (userResponse?.User == null)
				{
					PublishLoadFailed("Máy chủ không trả về thông tin tài khoản.");
					return;
				}

				var levelsResponse = ParseResponse<SettingLevelsResponsePayload>(await levelsTask);
				var storiesResponse = ParseResponse<SettingStoriesResponsePayload>(await storiesTask);

				SettingState.CurrentProfile = userResponse.User;
				SettingState.CachedLevels = levelsResponse?.Levels ?? new List<SettingLevelOptionPayload>();
				SettingState.CachedStories = storiesResponse?.Stories ?? new List<SettingStoryOptionPayload>();
				SettingState.CachedVoices = BuildVoiceOptions();

				PublishProfileLoaded();
			}
			catch (Exception exception)
			{
				PublishLoadFailed("Không thể tải cài đặt: " + exception.Message);
			}
		}

		private static async Task SaveProfileInternalAsync(SettingProfileSaveRequestPayload payload)
		{
			EventBus.Publish(SettingEvents.ProfileSaveStarted, null);

			try
			{
				var requestPayload = new SettingProfileUpdateRequestPayload
				{
					Name = payload.Name?.Trim(),
					Age = payload.Age,
					Description = payload.Description?.Trim(),
					LevelId = payload.LevelId,
					CurrentStoryId = payload.CurrentStoryId,
					VoiceName = payload.VoiceName?.Trim(),
					Pitch = payload.Pitch
				};

				var responseJson = await HttpClient.PutJsonTaskAsync(NetworkEndpoints.UserProfile, requestPayload);
				var response = ParseResponse<SettingUserResponsePayload>(responseJson);
				if (response?.User == null)
				{
					PublishSaveFailed("Máy chủ trả về dữ liệu không hợp lệ.");
					return;
				}

				SettingState.CurrentProfile = response.User;
				PublishProfileLoaded();
				EventBus.Publish(SettingEvents.ProfileSaveSucceeded, BuildProfilePayload());
			}
			catch (Exception exception)
			{
				PublishSaveFailed("Không thể lưu cài đặt: " + exception.Message);
			}
		}

		private static void PublishProfileLoaded()
		{
			EventBus.Publish(SettingEvents.ProfileLoaded, BuildProfilePayload());
		}

		private static SettingProfilePayload BuildProfilePayload()
		{
			var levels = SettingState.CachedLevels ?? new List<SettingLevelOptionPayload>();
			var stories = SettingState.CachedStories ?? new List<SettingStoryOptionPayload>();
			var voices = SettingState.CachedVoices ?? new List<SettingVoiceOptionPayload>();

			return new SettingProfilePayload
			{
				Username = SettingState.CurrentProfile?.Username,
				Name = SettingState.CurrentProfile?.Name,
				Age = SettingState.CurrentProfile?.Age,
				Description = SettingState.CurrentProfile?.Description,
				LevelId = SettingState.CurrentProfile?.LevelId,
				Levels = levels,
				CurrentStoryId = SettingState.CurrentProfile?.CurrentStoryId,
				Stories = stories,
				VoiceName = SettingState.CurrentProfile?.VoiceName,
				Voices = voices,
				Pitch = SettingState.CurrentProfile?.Pitch
			};
		}

		private static string ValidateSavePayload(SettingProfileSaveRequestPayload payload)
		{
			if (payload == null)
			{
				return "Thiếu dữ liệu cài đặt.";
			}

			if (string.IsNullOrWhiteSpace(payload.Name))
			{
				return "Tên không được để trống.";
			}

			if (payload.Age.HasValue && payload.Age.Value < 0)
			{
				return "Tuổi phải lớn hơn hoặc bằng 0.";
			}

			if (payload.LevelId.HasValue && payload.LevelId.Value <= 0)
			{
				return "Cấp độ không hợp lệ.";
			}

			if (payload.CurrentStoryId.HasValue && payload.CurrentStoryId.Value <= 0)
			{
				return "Câu chuyện không hợp lệ.";
			}

			return null;
		}

		private static void PublishLoadFailed(string message)
		{
			EventBus.Publish(SettingEvents.ProfileLoadFailed, new SettingErrorPayload { Message = message });
		}

		private static void PublishSaveFailed(string message)
		{
			EventBus.Publish(SettingEvents.ProfileSaveFailed, new SettingErrorPayload { Message = message });
		}

		private static T ParseResponse<T>(string json) where T : class
		{
			if (string.IsNullOrWhiteSpace(json))
			{
				return null;
			}

			try
			{
				return JsonConvert.DeserializeObject<T>(json);
			}
			catch
			{
				return null;
			}
		}

		private static List<SettingVoiceOptionPayload> BuildVoiceOptions()
		{
			var options = new List<SettingVoiceOptionPayload>();
			var names = SettingState.ParentSignals?.GetCharacterNames?.Invoke();
			if (names == null)
			{
				return options;
			}

			var seenVoices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var characterName in names)
			{
				if (string.IsNullOrWhiteSpace(characterName))
				{
					continue;
				}

				var voiceName = SettingState.ParentSignals?.GetCharacterVoiceName?.Invoke(characterName);
				if (string.IsNullOrWhiteSpace(voiceName) || !seenVoices.Add(voiceName))
				{
					continue;
				}

				var pitch = SettingState.ParentSignals?.GetCharacterPitch?.Invoke(characterName);
				options.Add(new SettingVoiceOptionPayload
				{
					CharacterName = characterName,
					VoiceName = voiceName,
					Pitch = pitch,
					Label = string.IsNullOrWhiteSpace(characterName)
						? voiceName
						: characterName + " (" + voiceName + ")"
				});
			}

			options.Sort((left, right) => string.Compare(left?.CharacterName, right?.CharacterName, StringComparison.OrdinalIgnoreCase));
			return options;
		}
	}
}
