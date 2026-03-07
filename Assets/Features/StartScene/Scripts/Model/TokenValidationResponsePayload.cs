namespace Features.StartScene.Model
{
	using Newtonsoft.Json;

	/// <summary>
	/// Response payload returned by token validation.
	/// </summary>
	[System.Serializable]
	public sealed class TokenValidationResponsePayload
	{
		/// <summary>
		/// True when token is valid.
		/// </summary>
		[JsonProperty("valid")]
		public bool Valid;

		/// <summary>
		/// New refresh token returned after validation.
		/// </summary>
		[JsonProperty("refreshToken")]
		public string RefreshToken;

		/// <summary>
		/// Error text in server error shape.
		/// </summary>
		[JsonProperty("error")]
		public string Error;

		/// <summary>
		/// Error text in server message shape.
		/// </summary>
		[JsonProperty("message")]
		public string Message;

		/// <summary>
		/// User data returned upon successful validation (if present).
		/// </summary>
		[JsonProperty("user")]
		public UserData User;
	}
}
