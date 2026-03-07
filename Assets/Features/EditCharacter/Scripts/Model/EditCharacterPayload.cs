using System;

namespace Features.EditCharacter.Model
{
	/// <summary>
	/// Payload for updating an existing character via server /api/characters/:id endpoint.
	/// </summary>
	[Serializable]
	public sealed class EditCharacterPayload
	{
		public int id;
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
	public sealed class EditCharacterAvatarUploadPayload
	{
		public string image;
		public string filename;
	}
}
