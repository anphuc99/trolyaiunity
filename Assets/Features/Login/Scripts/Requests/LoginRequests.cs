using System;

namespace Features.Login.Requests
{
	/// <summary>
	/// Request keys for the Login feature.
	/// </summary>
	public static class LoginRequests
	{
		/// <summary>Request key for standard login with username/password.</summary>
		public const string Login = "login.login.request";

		/// <summary>Request key for forgot password action.</summary>
		public const string ForgotPassword = "login.forgot-password.request";

		/// <summary>Request key for social login (payload: provider name string).</summary>
		public const string SocialLogin = "login.social-login.request";

		/// <summary>Request key for navigating to registration.</summary>
		public const string Register = "login.register.request";
	}
}
