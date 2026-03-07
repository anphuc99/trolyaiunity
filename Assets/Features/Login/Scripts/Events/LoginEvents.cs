using System;

namespace Features.Login.Events
{
	/// <summary>
	/// Event keys for the Login feature.
	/// </summary>
	public static class LoginEvents
	{
		/// <summary>Fired when login succeeds.</summary>
		public const string LoginSuccess = "login.login-success.event";

		/// <summary>Fired when login fails (payload: error message string).</summary>
		public const string LoginFailed = "login.login-failed.event";
	}
}
