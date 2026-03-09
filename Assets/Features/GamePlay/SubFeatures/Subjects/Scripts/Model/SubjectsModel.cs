using Newtonsoft.Json;

namespace Features.GamePlay.SubFeatures.Subjects.Model
{
	/// <summary>
	/// Payload representing a single subject item from the server.
	/// </summary>
	public sealed class SubjectItemPayload
	{
		/// <summary>
		/// Subject identifier.
		/// </summary>
		[JsonProperty("id")]
		public int Id { get; set; }

		/// <summary>
		/// Subject display name.
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
	/// Server response wrapper for the subjects list endpoint.
	/// </summary>
	public sealed class SubjectsResponsePayload
	{
		/// <summary>
		/// List of subjects returned by the server.
		/// </summary>
		[JsonProperty("subjects")]
		public System.Collections.Generic.List<SubjectItemPayload> Subjects { get; set; }
	}

	/// <summary>
	/// Error payload for Subjects feature communications.
	/// </summary>
	public sealed class SubjectsErrorPayload
	{
		/// <summary>
		/// Human-readable error message.
		/// </summary>
		public string Message { get; set; }
	}
}
