using System;

namespace Core.Infrastructure.Network
{
	/// <summary>
	/// Payload for token refresh request.
	/// </summary>
	[Serializable]
	public sealed class TokenRefreshRequest
	{
		public string RefreshToken;
	}

	/// <summary>
	/// Response for token refresh request.
	/// </summary>
	[Serializable]
	public sealed class TokenRefreshResponse
	{
		public string AccessToken;
		public string RefreshToken;
		public string Error;
	}

	/// <summary>
	/// Structure of error responses from the server.
	/// </summary>
	[Serializable]
	public sealed class AuthErrorResponse
	{
		public string Error;
		public string Code;
	}

	public static class AuthErrorCodes
	{
		public const string TokenExpired = "TOKEN_EXPIRED";
		public const string TokenInvalid = "TOKEN_INVALID";
	}
}
