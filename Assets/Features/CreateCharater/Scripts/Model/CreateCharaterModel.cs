using System.Collections.Generic;

namespace Features.CreateCharater.Model
{
	[System.Serializable]
	public sealed class VoiceOptionData
	{
		public int id;
		public string model;
		public string voice;
	}

	[System.Serializable]
	public sealed class VoiceOptionsResponse
	{
		public List<VoiceOptionData> voices;
	}

	/// <summary>
	/// Data model for CreateCharater.
	/// </summary>
	public static class CreateCharaterModel
	{
		/// <summary>
		/// List of available personalities fetched from the server.
		/// </summary>
		public static List<PersonalityData> AvailablePersonalities { get; set; } = new List<PersonalityData>();

		/// <summary>
		/// List of available voice options fetched from DB.
		/// </summary>
		public static List<VoiceOptionData> AvailableVoices { get; set; } = new List<VoiceOptionData>();
	}
}
