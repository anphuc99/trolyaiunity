using System;
using System.Collections.Generic;

namespace Features.CreateCharater.Model
{
	/// <summary>
	/// Payload for creating a new character.
	/// </summary>
	[Serializable]
	public sealed class CreateCharacterPayload
	{
		public string name;
		public int age;
		public string gender;
		public List<string> personality = new List<string>();
		public string description;
	}
}
