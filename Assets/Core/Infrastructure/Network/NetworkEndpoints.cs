namespace Core.Infrastructure.Network
{
	/// <summary>
	/// Contains all network API endpoint paths.
	/// </summary>
	public static class NetworkEndpoints
	{
		public const string Health = "/api/health";
		public const string Version = "/api/version";
		public const string Login = "/api/users/login";
		public const string TokenValidate = "/api/token/validate";
		public const string TokenRefresh = "/api/token/refresh";
		public const string Characters = "/api/characters";
		public const string CharactersUploadAvatar = "/api/characters/upload-avatar";
		public const string Personalities = "/personalities";
	}
}
