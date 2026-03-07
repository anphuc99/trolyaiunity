namespace Features.Login.Model
{
	using Newtonsoft.Json;

	/// <summary>
	/// Payload used to submit login credentials.
	/// </summary>
	[System.Serializable]
	public sealed class LoginRequestPayload
	{
		/// <summary>
		/// Username value.
		/// </summary>
		[JsonProperty("username")]
		public string Username;

		/// <summary>
		/// Password value.
		/// </summary>
		[JsonProperty("password")]
		public string Password;
	}
}
