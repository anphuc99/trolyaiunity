using System;

namespace Features.CreateCharater.Model
{
	/// <summary>
	/// Payload for creating a character via server /api/characters endpoint.
	/// </summary>
	[Serializable]
	public sealed class CreateCharacterPayload
	{
		public string name;
		public int? age;
		public string personality;
		public string gender;
		public string appearance;
		public string avatar;
		public string voiceModel;
		public string voiceName;
		public float? pitch;
		public float? speakingRate;
	}

	/// <summary>
	/// Payload used by character avatar upload endpoint.
	/// </summary>
	[Serializable]
	public sealed class AvatarUploadPayload
	{
		public string image;
		public string filename;
	}
}
