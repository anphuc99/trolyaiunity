namespace Features.Login.Model
{
	using Newtonsoft.Json;

	/// <summary>
	/// Response payload returned by the login endpoint.
	/// </summary>
	[System.Serializable]
	public sealed class LoginResponsePayload
	{
		/// <summary>
		/// Short-lived JWT token.
		/// </summary>
		[JsonProperty("accessToken")]
		public string AccessToken;

		/// <summary>
		/// Long-lived refresh token.
		/// </summary>
		[JsonProperty("refreshToken")]
		public string RefreshToken;

		/// <summary>
		/// User role.
		/// </summary>
		public int Role;

		/// <summary>
		/// Backward compatibility for existing code using .Token.
		/// </summary>
		public string Token => AccessToken;

		/// <summary>
		/// Error message when login fails.
		/// </summary>
		[JsonProperty("error")]
		public string Error;

		/// <summary>
		/// Message field returned by server for many error responses.
		/// </summary>
		[JsonProperty("message")]
		public string Message;
	}
}
