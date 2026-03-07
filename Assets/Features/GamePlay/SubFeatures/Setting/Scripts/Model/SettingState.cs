using System.Collections.Generic;

namespace Features.GamePlay.SubFeatures.Setting.Model
{
	/// <summary>
	/// Holds subfeature state, including parent signal bindings and cached payloads.
	/// </summary>
	public static class SettingState
	{
		public static SettingParentSignals ParentSignals { get; set; }
		public static SettingUserProfilePayload CurrentProfile { get; set; }
		public static List<SettingLevelOptionPayload> CachedLevels { get; set; }
		public static List<SettingStoryOptionPayload> CachedStories { get; set; }
		public static List<SettingVoiceOptionPayload> CachedVoices { get; set; }

		/// <summary>
		/// Clears all cached state.
		/// </summary>
		public static void Reset()
		{
			ParentSignals = null;
			CurrentProfile = null;
			CachedLevels = null;
			CachedStories = null;
			CachedVoices = null;
		}
	}
}
