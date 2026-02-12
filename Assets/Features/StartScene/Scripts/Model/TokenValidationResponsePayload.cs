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
		/// Error message when token is rejected.
		/// </summary>
		public string Error;
	}
}
