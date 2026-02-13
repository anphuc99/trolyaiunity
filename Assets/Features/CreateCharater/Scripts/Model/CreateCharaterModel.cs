using System.Collections.Generic;

namespace Features.CreateCharater.Model
{
	/// <summary>
	/// Data model for CreateCharater.
	/// </summary>
	public static class CreateCharaterModel
	{
		/// <summary>
		/// List of available personalities fetched from the server.
		/// </summary>
		public static List<PersonalityData> AvailablePersonalities { get; set; } = new List<PersonalityData>();
	}
}
