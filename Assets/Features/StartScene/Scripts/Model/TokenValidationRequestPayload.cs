namespace Features.StartScene.Model
{
	/// <summary>
	/// Payload used to validate an auth token.
	/// </summary>
	[System.Serializable]
	public sealed class TokenValidationRequestPayload
	{
		/// <summary>
		/// JWT token to validate.
		/// </summary>
		public string Token;
	}
}
