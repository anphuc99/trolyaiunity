namespace Features.Login.Model
{
	/// <summary>
	/// Payload used to submit login credentials.
	/// </summary>
	[System.Serializable]
	public sealed class LoginRequestPayload
	{
		/// <summary>
		/// Username value.
		/// </summary>
		public string Username;

		/// <summary>
		/// Password value.
		/// </summary>
		public string Password;
	}
}
