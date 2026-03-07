using System;

namespace Features.CreateCharater.Model
{
	/// <summary>
	/// Represents personality data returned from the server.
	/// </summary>
	[Serializable]
	public sealed class PersonalityData
	{
		public int id;
		public string name;
		public string description;
	}
}
