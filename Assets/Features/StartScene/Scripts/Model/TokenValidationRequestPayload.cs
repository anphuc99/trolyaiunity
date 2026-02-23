namespace Features.StartScene.Model
{
	using Newtonsoft.Json;

	/// <summary>
	/// Payload used to validate an auth token.
	/// </summary>
	[System.Serializable]
	public sealed class TokenValidationRequestPayload
	{
		/// <summary>
		/// Refresh token to validate.
		/// </summary>
		[JsonProperty("refreshToken")]
		public string RefreshToken;
	}
}
