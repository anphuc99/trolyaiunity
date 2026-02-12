namespace Features.Login.Model
{
	/// <summary>
	/// Response payload returned by the login endpoint.
	/// </summary>
	[System.Serializable]
	public sealed class LoginResponsePayload
	{
		/// <summary>
		/// JWT token when login succeeds.
		/// </summary>
		public string Token;

		/// <summary>
		/// Error message when login fails.
		/// </summary>
		public string Error;
	}
}
