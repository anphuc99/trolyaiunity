using System.Collections.Generic;

namespace Features.EditCharacter.Model
{
	[System.Serializable]
	public sealed class EditVoiceOptionData
	{
		public int id;
		public string model;
		public string voice;
	}

	[System.Serializable]
	public sealed class EditVoiceOptionsResponse
	{
		public List<EditVoiceOptionData> voices;
	}

	/// <summary>
	/// Data model for EditCharacter.
	/// </summary>
	public static class EditCharacterModel
	{
		/// <summary>
		/// List of available voice options fetched from DB.
		/// </summary>
		public static List<EditVoiceOptionData> AvailableVoices { get; set; } = new List<EditVoiceOptionData>();
	}
}
