namespace Features.StartScene.Model
{
	/// <summary>
	/// Response payload returned by token validation.
	/// </summary>
	[System.Serializable]
	public sealed class TokenValidationResponsePayload
	{
		/// <summary>
		/// True when token is valid.
		/// </summary>
		public bool Valid;

		/// <summary>
		/// New access token returned after validation.
		/// </summary>
		public string AccessToken;

		/// <summary>
		/// New refresh token returned after validation.
		/// </summary>
		public string RefreshToken;

		/// <summary>
		/// User data returned upon successful validation.
		/// </summary>
		public UserData User;

		/// <summary>
		/// Error message when token is rejected.
		/// </summary>
		public string Error;
	}
}
