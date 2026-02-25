using System.Threading.Tasks;
using System.Collections.Generic;
using Features.GamePlay.Events;
using Features.GamePlay.Infrastructure;
using Features.GamePlay.Infrastructure.Attributes;
using Features.GamePlay.Model;
using Features.GamePlay.Requests;
using Features.GamePlay.SubFeatures.Character.Controller;
using Features.GamePlay.SubFeatures.Character.Model;
using Features.GamePlay.SubFeatures.Chat.Controller;
using Features.GamePlay.SubFeatures.Chat.Model;
using Features.GamePlay.SubFeatures.Home.Controller;
using Features.GamePlay.SubFeatures.Home.Model;
using Features.GamePlay.SubFeatures.Journal.Controller;
using Features.GamePlay.SubFeatures.Journal.Model;
using Features.GamePlay.SubFeatures.Practice.Controller;
using Features.GamePlay.SubFeatures.Practice.Model;
using Features.GamePlay.SubFeatures.Story.Controller;
using Features.GamePlay.SubFeatures.Story.Model;
using Features.GamePlay.SubFeatures.Task.Controller;
using Features.GamePlay.SubFeatures.Task.Model;
using Core.Infrastructure.Network;
using Newtonsoft.Json;
using Share.Model;
using UnityEngine.Networking;
using Core.Infrastructure.Scenes;
using UnityEngine.SceneManagement;

namespace Features.GamePlay.Controller
{
	/// <summary>
	/// Controller for GamePlay.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class GamePlayController
	{
		/// <summary>
		/// Called when the controller scope is entered.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerInit]
		public static async void OnEnterScope()
		{
			await LoadChatCharactersCacheAsync();
			SetAllSubControllerSignals();
			await Task.Yield();
			InstallSubController(GamePlaySubControllerType.Home);
		}

		/// <summary>
		/// Called when the controller scope is exited.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerShutdown]
		public static void OnExitScope()
		{
			CloseCurrentSubControllerInternal();
			ClearAllSubControllerSignals();
			ClearChatCharacterCache();
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(GamePlayRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(GamePlayEvents.Echoed, payload);
		}

		/// <summary>
		/// Opens a subcontroller by name or enum value.
		/// Automatically closes the currently opened subcontroller first.
		/// </summary>
		/// <param name="payload">Subcontroller name or <see cref="GamePlaySubControllerType"/> value.</param>
		/// <returns>True when opened successfully; otherwise false.</returns>
		[Request(GamePlayRequests.OpenSubController)]
		public static bool HandleOpenSubController(object payload)
		{
			if (!TryResolveSubController(payload, out var target))
			{
				return false;
			}

			if (GamePlayState.CurrentSubController == target)
			{
				return true;
			}

			CloseCurrentSubControllerInternal();
			InstallSubController(target);
			EventBus.Publish(GamePlayEvents.SubControllerChanged, target.ToString());
			return true;
		}

		/// <summary>
		/// Closes the currently opened subcontroller.
		/// </summary>
		[Request(GamePlayRequests.CloseCurrentSubController)]
		public static void HandleCloseCurrentSubController()
		{
			CloseCurrentSubControllerInternal();
		}

		/// <summary>
		/// Gets the currently active subcontroller name.
		/// </summary>
		/// <returns>Current subcontroller name, or null if none is active.</returns>
		[Request(GamePlayRequests.GetCurrentSubController)]
		public static string HandleGetCurrentSubController()
		{
			return GamePlayState.CurrentSubController?.ToString();
		}

		/// <summary>
		/// Installs a subcontroller by binding parent signals.
		/// </summary>
		/// <param name="subControllerType">Subcontroller to install.</param>
		private static void InstallSubController(GamePlaySubControllerType subControllerType)
		{
			switch (subControllerType)
			{
				case GamePlaySubControllerType.Home:
					HomeController.Install();
					break;
				case GamePlaySubControllerType.Character:
					CharacterController.Install();
					break;
				case GamePlaySubControllerType.Chat:
					ChatController.Install();
					break;
				case GamePlaySubControllerType.Journal:
					JournalController.Install();
					break;
				case GamePlaySubControllerType.Practice:
					PracticeController.Install();
					break;
				case GamePlaySubControllerType.Story:
					StoryController.Install();
					break;
				case GamePlaySubControllerType.Task:
					TaskController.Install();
					break;
			}
			GamePlayState.CurrentSubController = subControllerType;
		}

		/// <summary>
		/// Sets parent signals for all subcontrollers when entering gameplay scope.
		/// </summary>
		private static void SetAllSubControllerSignals()
		{
			HomeController.SetParentSignals(new HomeParentSignals { 
				OnEchoed = OnSubControllerEchoed,
				OpenJournal = () => HandleOpenSubController(GamePlaySubControllerType.Journal), 
				OpenCreateCharacter = HandleOpenCreateCharacter,
				OpenCharacter = () => HandleOpenSubController(GamePlaySubControllerType.Character),
			});
			CharacterController.SetParentSignals(new CharacterParentSignals
			{
				OnEchoed = OnSubControllerEchoed,
				GetCharacterNames = GetChatCharacterNames,
				GetCharacterAvatar = GetChatCharacterAvatar,
				GetCharacterInfo = GetChatCharacterInfo,
			});
			ChatController.SetParentSignals(new ChatParentSignals
			{
				OnEchoed = OnSubControllerEchoed,
				GetCharacterAvatarByName = GetChatCharacterAvatar,
				GetCharacterVoiceNameByName = GetChatCharacterVoiceName,
				GetCharacterPitchByName = GetChatCharacterPitch,
				GetCharacterSpeakingRateByName = GetChatCharacterSpeakingRate,
			});
			JournalController.SetParentSignals(new JournalParentSignals { 
				OnEchoed = OnSubControllerEchoed,
				GetAvatar = GetChatCharacterAvatar, 
			});
			PracticeController.SetParentSignals(new PracticeParentSignals { OnEchoed = OnSubControllerEchoed });
			StoryController.SetParentSignals(new StoryParentSignals { OnEchoed = OnSubControllerEchoed });
			TaskController.SetParentSignals(new TaskParentSignals { OnEchoed = OnSubControllerEchoed });
		}

		private static UnityEngine.Sprite GetChatCharacterAvatar(string characterName)
		{
			if (string.IsNullOrWhiteSpace(characterName))
			{
				return null;
			}

			return GamePlayState.ChatCharacterByName.TryGetValue(characterName.Trim(), out var cachedCharacter)
				? cachedCharacter.AvatarSprite
				: null;
		}

		private static List<string> GetChatCharacterNames()
		{
			var names = new List<string>();
			foreach (var pair in GamePlayState.ChatCharacterByName)
			{
				if (string.IsNullOrWhiteSpace(pair.Key))
				{
					continue;
				}

				names.Add(pair.Key);
			}

			names.Sort(System.StringComparer.OrdinalIgnoreCase);
			return names;
		}

		private static string GetChatCharacterVoiceName(string characterName)
		{
			if (string.IsNullOrWhiteSpace(characterName))
			{
				return null;
			}

			return GamePlayState.ChatCharacterByName.TryGetValue(characterName.Trim(), out var cachedCharacter)
				? cachedCharacter.VoiceName
				: null;
		}

		private static SelectedCharacterInfo GetChatCharacterInfo(string characterName)
		{
			if (string.IsNullOrWhiteSpace(characterName))
			{
				return null;
			}

			if (!GamePlayState.ChatCharacterByName.TryGetValue(characterName.Trim(), out var cachedCharacter)
				|| cachedCharacter == null)
			{
				return null;
			}

			return new SelectedCharacterInfo
			{
				Name = cachedCharacter.Name,
				Avatar = cachedCharacter.AvatarSprite,
				Age = cachedCharacter.Age,
				Description = cachedCharacter.Personality,
				Gender = cachedCharacter.Gender,
				VoiceName = cachedCharacter.VoiceName,
				Pitch = cachedCharacter.Pitch,
			};
		}

		private static float? GetChatCharacterPitch(string characterName)
		{
			if (string.IsNullOrWhiteSpace(characterName))
			{
				return null;
			}

			return GamePlayState.ChatCharacterByName.TryGetValue(characterName.Trim(), out var cachedCharacter)
				? cachedCharacter.Pitch
				: null;
		}

		private static float? GetChatCharacterSpeakingRate(string characterName)
		{
			if (string.IsNullOrWhiteSpace(characterName))
			{
				return null;
			}

			return GamePlayState.ChatCharacterByName.TryGetValue(characterName.Trim(), out var cachedCharacter)
				? cachedCharacter.SpeakingRate
				: null;
		}

		private static void ClearChatCharacterCache()
		{
			GamePlayState.ChatCharacterByName.Clear();
		}

		private static async Task LoadChatCharactersCacheAsync()
		{
			ClearChatCharacterCache();

			try
			{
				var responseJson = await HttpClient.GetTaskAsync(NetworkEndpoints.Characters);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					return;
				}

				var characters = JsonConvert.DeserializeObject<List<GamePlayChatCharacterPayload>>(responseJson);
				if (characters == null)
				{
					return;
				}

				for (var i = 0; i < characters.Count; i++)
				{
					var character = characters[i];
					if (character == null || string.IsNullOrWhiteSpace(character.Name))
					{
						continue;
					}

					UnityEngine.Sprite sprite = null;
					if (!string.IsNullOrWhiteSpace(character.Avatar))
					{
						try
						{
							sprite = await LoadSpriteFromUrlAsync(character.Avatar.Trim());
						}
						catch (System.Exception exception)
						{
							UnityEngine.Debug.LogError("[GamePlayController] Failed to load avatar for character '" + character.Name + "' from URL '" + character.Avatar + "': " + exception);
						}
					}

					var key = character.Name.Trim();
					if (string.IsNullOrWhiteSpace(key))
					{
						continue;
					}

					GamePlayState.ChatCharacterByName[key] = new GamePlayChatCharacterCache
					{
						Id = character.Id,
						Name = key,
						Personality = character.Personality,
						Gender = character.Gender,
						Age = character.Age,
						Appearance = character.Appearance,
						AvatarUrl = character.Avatar,
						VoiceModel = character.VoiceModel,
						VoiceName = character.VoiceName,
						Pitch = character.Pitch,
						SpeakingRate = character.SpeakingRate,
						CreatedAt = character.CreatedAt,
						UpdatedAt = character.UpdatedAt,
						AvatarSprite = sprite,
					};
				}
			}
			catch (System.Exception exception)
			{
				UnityEngine.Debug.LogError("[GamePlayController] Failed to load chat character cache: " + exception);
			}
		}

		private static async Task<UnityEngine.Sprite> LoadSpriteFromUrlAsync(string avatarUrl)
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
				UnityEngine.Debug.LogError("[GamePlayController] Failed to start avatar request for URL '" + avatarUrl + "': " + exception);
				return null;
			}
			while (!operation.isDone)
			{
				await Task.Yield();
			}

			if (request.result != UnityWebRequest.Result.Success)
			{
				UnityEngine.Debug.LogError("[GamePlayController] Failed to load avatar sprite from URL '" + avatarUrl + "': " + request.error);
				return null;
			}

			var texture = DownloadHandlerTexture.GetContent(request);
			if (texture == null)
			{
				return null;
			}

			return UnityEngine.Sprite.Create(texture, new UnityEngine.Rect(0f, 0f, texture.width, texture.height), new UnityEngine.Vector2(0.5f, 0.5f));
		}

		private sealed class GamePlayChatCharacterPayload
		{
			[JsonProperty("id")]
			public int Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("personality")]
			public string Personality { get; set; }

			[JsonProperty("gender")]
			public string Gender { get; set; }

			[JsonProperty("age")]
			public int? Age { get; set; }

			[JsonProperty("appearance")]
			public string Appearance { get; set; }

			[JsonProperty("avatar")]
			public string Avatar { get; set; }

			[JsonProperty("voiceModel")]
			public string VoiceModel { get; set; }

			[JsonProperty("voiceName")]
			public string VoiceName { get; set; }

			[JsonProperty("pitch")]
			public float? Pitch { get; set; }

			[JsonProperty("speakingRate")]
			public float? SpeakingRate { get; set; }

			[JsonProperty("createdAt")]
			public string CreatedAt { get; set; }

			[JsonProperty("updatedAt")]
			public string UpdatedAt { get; set; }
		}

		/// <summary>
		/// Clears parent signals for all subcontrollers when exiting gameplay scope.
		/// </summary>
		private static void ClearAllSubControllerSignals()
		{
			HomeController.SetParentSignals(null);
			CharacterController.SetParentSignals(null);
			ChatController.SetParentSignals(null);
			JournalController.SetParentSignals(null);
			PracticeController.SetParentSignals(null);
			StoryController.SetParentSignals(null);
			TaskController.SetParentSignals(null);
		}

		/// <summary>
		/// Uninstalls a subcontroller by unbinding parent signals.
		/// </summary>
		/// <param name="subControllerType">Subcontroller to uninstall.</param>
		private static void UninstallSubController(GamePlaySubControllerType subControllerType)
		{
			switch (subControllerType)
			{
				case GamePlaySubControllerType.Home:
					HomeController.Uninstall();
					break;
				case GamePlaySubControllerType.Character:
					CharacterController.Uninstall();
					break;
				case GamePlaySubControllerType.Chat:
					ChatController.Uninstall();
					break;
				case GamePlaySubControllerType.Journal:
					JournalController.Uninstall();
					break;
				case GamePlaySubControllerType.Practice:
					PracticeController.Uninstall();
					break;
				case GamePlaySubControllerType.Story:
					StoryController.Uninstall();
					break;
				case GamePlaySubControllerType.Task:
					TaskController.Uninstall();
					break;
			}
		}

		/// <summary>
		/// Closes and uninstalls the current subcontroller if one is active.
		/// </summary>
		private static void CloseCurrentSubControllerInternal()
		{
			if (!GamePlayState.CurrentSubController.HasValue)
			{
				return;
			}

			UninstallSubController(GamePlayState.CurrentSubController.Value);
			GamePlayState.CurrentSubController = null;
			EventBus.Publish(GamePlayEvents.SubControllerChanged, null);
		}

		/// <summary>
		/// Tries to resolve a subcontroller value from request payload.
		/// </summary>
		/// <param name="payload">Incoming request payload.</param>
		/// <param name="subControllerType">Resolved subcontroller type.</param>
		/// <returns>True if payload maps to a valid subcontroller.</returns>
		private static bool TryResolveSubController(object payload, out GamePlaySubControllerType subControllerType)
		{
			if (payload is GamePlaySubControllerType typed)
			{
				subControllerType = typed;
				return true;
			}

			if (payload is string text && !string.IsNullOrWhiteSpace(text))
			{
				return System.Enum.TryParse(text.Trim(), ignoreCase: true, out subControllerType);
			}

			subControllerType = default;
			return false;
		}

		/// <summary>
		/// Handles echo signals from installed subcontrollers.
		/// </summary>
		/// <param name="payload">Signal payload.</param>
		private static void OnSubControllerEchoed(object payload)
		{
			EventBus.Publish(GamePlayEvents.Echoed, payload);
		}

		private static void HandleOpenCreateCharacter()
		{
			LoadScene.ByScope(Core.Infrastructure.Attributes.ControllerScopeKey.CreateCharaterGameplay, LoadSceneMode.Additive);
		}
	}
}
