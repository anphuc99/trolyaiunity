namespace Core.Infrastructure.Authentication
{
	/// <summary>
	/// Stores the current auth token for the session.
	/// </summary>
	public static class AuthTokenModel
	{
		/// <summary>
		/// Current JWT token value.
		/// </summary>
		public static string Token { get; set; }
	}
}
