namespace Core.Infrastructure.Network
{
	/// <summary>
	/// Contains all network API endpoint paths.
	/// </summary>
	public static class NetworkEndpoints
	{
		public const string Health = "/health";
		public const string Version = "/version";
		public const string Login = "/login";
		public const string TokenValidate = "/token/validate";
		public const string Characters = "/characters";
	}
}
