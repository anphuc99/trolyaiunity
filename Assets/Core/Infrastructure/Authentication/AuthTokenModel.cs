using UnityEngine;

namespace Core.Infrastructure.Authentication
{
	/// <summary>
	/// Stores the current auth token for the session.
	/// </summary>
	public static class AuthTokenModel
	{
		private const string RefreshTokenKey = "RefreshToken";
		private static string _accessToken;
		private static string _refreshToken;

		/// <summary>
		/// Short-lived JWT token value (not persisted).
		/// </summary>
		public static string AccessToken
		{
			get => _accessToken;
			set => _accessToken = value;
		}

		/// <summary>
		/// Long-lived Refresh token value (persisted).
		/// </summary>
		public static string RefreshToken
		{
			get
			{
				if (_refreshToken == null)
				{
					_refreshToken = PlayerPrefs.GetString(RefreshTokenKey, null);
				}
				return _refreshToken;
			}
			set
			{
				_refreshToken = value;
				if (string.IsNullOrEmpty(value))
				{
					PlayerPrefs.DeleteKey(RefreshTokenKey);
				}
				else
				{
					PlayerPrefs.SetString(RefreshTokenKey, value);
				}
				PlayerPrefs.Save();
			}
		}

		/// <summary>
		/// Backward compatibility for existing code using .Token.
		/// Maps to RefreshToken.
		/// </summary>
		public static string Token
		{
			get => RefreshToken;
			set => RefreshToken = value;
		}
	}
}
