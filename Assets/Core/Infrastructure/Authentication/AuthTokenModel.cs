using UnityEngine;

namespace Core.Infrastructure.Authentication
{
	/// <summary>
	/// Stores the current auth token for the session.
	/// </summary>
	public static class AuthTokenModel
	{
		private const string TokenKey = "AuthToken";
		private static string _token;

		/// <summary>
		/// Current JWT token value.
		/// </summary>
		public static string Token
		{
			get
			{
				if (_token == null)
				{
					_token = PlayerPrefs.GetString(TokenKey, null);
				}
				return _token;
			}
			set
			{
				_token = value;
				if (string.IsNullOrEmpty(value))
				{
					PlayerPrefs.DeleteKey(TokenKey);
				}
				else
				{
					PlayerPrefs.SetString(TokenKey, value);
				}
				PlayerPrefs.Save();
			}
		}
	}
}
