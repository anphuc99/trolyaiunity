using System.Collections.Generic;
using Newtonsoft.Json;

namespace Features.GamePlay.SubFeatures.Setting.Model
{
	/// <summary>
	/// Raw user profile payload returned by the server.
	/// </summary>
	public sealed class SettingUserProfilePayload
	{
		[JsonProperty("id")]
		public int Id { get; set; }

		[JsonProperty("username")]
		public string Username { get; set; }

		[JsonProperty("name")]
		public string Name { get; set; }

		[JsonProperty("age")]
		public int? Age { get; set; }

		[JsonProperty("description")]
		public string Description { get; set; }

		[JsonProperty("levelId")]
		public int? LevelId { get; set; }

		[JsonProperty("currentStoryId")]
		public int? CurrentStoryId { get; set; }

		[JsonProperty("voiceName")]
		public string VoiceName { get; set; }

		[JsonProperty("pitch")]
		public float? Pitch { get; set; }
	}

	/// <summary>
	/// Level dropdown option.
	/// </summary>
	public sealed class SettingLevelOptionPayload
	{
		[JsonProperty("id")]
		public int Id { get; set; }

		[JsonProperty("level")]
		public string Name { get; set; }

		[JsonProperty("descript")]
		public string Description { get; set; }
	}

	/// <summary>
	/// Story dropdown option.
	/// </summary>
	public sealed class SettingStoryOptionPayload
	{
		[JsonProperty("id")]
		public int Id { get; set; }

		[JsonProperty("name")]
		public string Name { get; set; }
	}

	/// <summary>
	/// Voice dropdown option built from cached characters.
	/// </summary>
	public sealed class SettingVoiceOptionPayload
	{
		public string CharacterName { get; set; }
		public string VoiceName { get; set; }
		public float? Pitch { get; set; }
		public string Label { get; set; }
	}

	/// <summary>
	/// Aggregated view payload for the Setting screen.
	/// </summary>
	public sealed class SettingProfilePayload
	{
		public string Username { get; set; }
		public string Name { get; set; }
		public int? Age { get; set; }
		public string Description { get; set; }
		public int? LevelId { get; set; }
		public IReadOnlyList<SettingLevelOptionPayload> Levels { get; set; }
		public int? CurrentStoryId { get; set; }
		public IReadOnlyList<SettingStoryOptionPayload> Stories { get; set; }
		public string VoiceName { get; set; }
		public IReadOnlyList<SettingVoiceOptionPayload> Voices { get; set; }
		public float? Pitch { get; set; }
	}

	/// <summary>
	/// Request payload sent by the view when saving user settings.
	/// </summary>
	public sealed class SettingProfileSaveRequestPayload
	{
		public string Name { get; set; }
		public int? Age { get; set; }
		public string Description { get; set; }
		public int? LevelId { get; set; }
		public int? CurrentStoryId { get; set; }
		public string VoiceName { get; set; }
		public float Pitch { get; set; }
	}

	/// <summary>
	/// JSON payload sent to the server.
	/// </summary>
	public sealed class SettingProfileUpdateRequestPayload
	{
		[JsonProperty("name")]
		public string Name { get; set; }

		[JsonProperty("age")]
		public int? Age { get; set; }

		[JsonProperty("description")]
		public string Description { get; set; }

		[JsonProperty("levelId")]
		public int? LevelId { get; set; }

		[JsonProperty("currentStoryId")]
		public int? CurrentStoryId { get; set; }

		[JsonProperty("voiceName")]
		public string VoiceName { get; set; }

		[JsonProperty("pitch")]
		public float? Pitch { get; set; }
	}

	/// <summary>
	/// Wrapper for /api/users/me responses.
	/// </summary>
	internal sealed class SettingUserResponsePayload
	{
		[JsonProperty("user")]
		public SettingUserProfilePayload User { get; set; }
	}

	/// <summary>
	/// Wrapper for /api/levels responses.
	/// </summary>
	internal sealed class SettingLevelsResponsePayload
	{
		[JsonProperty("levels")]
		public List<SettingLevelOptionPayload> Levels { get; set; }
	}

	/// <summary>
	/// Wrapper for /api/stories responses.
	/// </summary>
	internal sealed class SettingStoriesResponsePayload
	{
		[JsonProperty("stories")]
		public List<SettingStoryOptionPayload> Stories { get; set; }
	}

	/// <summary>
	/// Standardized error payload for Setting events.
	/// </summary>
	public sealed class SettingErrorPayload
	{
		public string Message { get; set; }
	}
}
