using System;
using Newtonsoft.Json;

namespace Core.Infrastructure.Network
{
	/// <summary>
	/// Payload for token refresh request.
	/// </summary>
	[Serializable]
	public sealed class TokenRefreshRequest
	{
		[JsonProperty("refreshToken")]
		public string RefreshToken;
	}

	/// <summary>
	/// Response for token refresh request.
	/// </summary>
	[Serializable]
	public sealed class TokenRefreshResponse
	{
		[JsonProperty("accessToken")]
		public string AccessToken;
		[JsonProperty("refreshToken")]
		public string RefreshToken;
		[JsonProperty("error")]
		public string Error;
		[JsonProperty("message")]
		public string Message;
	}

	/// <summary>
	/// Structure of error responses from the server.
	/// </summary>
	[Serializable]
	public sealed class AuthErrorResponse
	{
		[JsonProperty("error")]
		public string Error;
		[JsonProperty("code")]
		public string Code;
		[JsonProperty("message")]
		public string Message;
	}

	public static class AuthErrorCodes
	{
		public const string TokenExpired = "TOKEN_EXPIRED";
		public const string TokenInvalid = "TOKEN_INVALID";
	}
}
