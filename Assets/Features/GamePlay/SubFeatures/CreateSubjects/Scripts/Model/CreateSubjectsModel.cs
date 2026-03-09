using Newtonsoft.Json;

namespace Features.GamePlay.SubFeatures.CreateSubjects.Model
{
	/// <summary>
	/// Payload sent to the server when creating a new subject.
	/// </summary>
	public sealed class CreateSubjectPayload
	{
		/// <summary>
		/// Subject name.
		/// </summary>
		[JsonProperty("name")]
		public string Name { get; set; }

		/// <summary>
		/// Optional subject description.
		/// </summary>
		[JsonProperty("description")]
		public string Description { get; set; }
	}

	/// <summary>
	/// Response from the server after creating a subject.
	/// </summary>
	public sealed class CreateSubjectResponsePayload
	{
		/// <summary>
		/// Server-assigned subject id.
		/// </summary>
		[JsonProperty("id")]
		public int Id { get; set; }

		/// <summary>
		/// Subject name.
		/// </summary>
		[JsonProperty("name")]
		public string Name { get; set; }

		/// <summary>
		/// Subject description.
		/// </summary>
		[JsonProperty("description")]
		public string Description { get; set; }
	}

	/// <summary>
	/// Error payload for create subject failures.
	/// </summary>
	public sealed class CreateSubjectsErrorPayload
	{
		/// <summary>
		/// Error message to display.
		/// </summary>
		public string Message { get; set; }
	}
}
