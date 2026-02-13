namespace Features.Login.Model
{
	/// <summary>
	/// Response payload returned by the login endpoint.
	/// </summary>
	[System.Serializable]
	public sealed class LoginResponsePayload
	{
		/// <summary>
		/// Short-lived JWT token.
		/// </summary>
		public string AccessToken;

		/// <summary>
		/// Long-lived refresh token.
		/// </summary>
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
		public string Error;
	}
}
