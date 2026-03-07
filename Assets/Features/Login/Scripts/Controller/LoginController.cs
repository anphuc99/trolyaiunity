using Features.Login.Events;
using Features.Login.Infrastructure;
using Features.Login.Infrastructure.Attributes;
using Features.Login.Requests;
using Features.Login.View;

namespace Features.Login.Controller
{
	/// <summary>
	/// Controller for Login. Handles login requests and publishes result events.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.LoginGameplay)]
	public static class LoginController
	{
		/// <summary>
		/// Called when the controller scope is entered.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerInit]
		public static void OnEnterScope()
		{
		}

		/// <summary>
		/// Called when the controller scope is exited.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerShutdown]
		public static void OnExitScope()
		{
		}

		/// <summary>
		/// Handles standard login request with username/password.
		/// Validates input and publishes success or failure event.
		/// </summary>
		/// <param name="payload">LoginPayload containing username and password.</param>
		[Request(LoginRequests.Login)]
		public static void HandleLogin(object payload)
		{
			if (payload is not LoginPayload loginPayload)
			{
				EventBus.Publish(LoginEvents.LoginFailed, "Invalid login payload.");
				return;
			}

			if (string.IsNullOrWhiteSpace(loginPayload.Username))
			{
				EventBus.Publish(LoginEvents.LoginFailed, "Tên đăng nhập không được để trống.");
				return;
			}

			if (string.IsNullOrWhiteSpace(loginPayload.Password))
			{
				EventBus.Publish(LoginEvents.LoginFailed, "Mật mã không được để trống.");
				return;
			}

			// TODO: Integrate with actual authentication backend
			EventBus.Publish(LoginEvents.LoginSuccess, loginPayload.Username);
		}

		/// <summary>
		/// Handles forgot password request.
		/// </summary>
		/// <param name="payload">Optional payload (unused).</param>
		[Request(LoginRequests.ForgotPassword)]
		public static void HandleForgotPassword(object payload)
		{
			// TODO: Navigate to forgot password flow
		}

		/// <summary>
		/// Handles social login request.
		/// </summary>
		/// <param name="payload">Provider name string (e.g. "google", "facebook", "apple").</param>
		[Request(LoginRequests.SocialLogin)]
		public static void HandleSocialLogin(object payload)
		{
			string provider = payload as string;
			if (string.IsNullOrWhiteSpace(provider))
			{
				EventBus.Publish(LoginEvents.LoginFailed, "Invalid social login provider.");
				return;
			}

			// TODO: Integrate with social authentication SDKs
			EventBus.Publish(LoginEvents.LoginSuccess, provider);
		}

		/// <summary>
		/// Handles register navigation request.
		/// </summary>
		/// <param name="payload">Optional payload (unused).</param>
		[Request(LoginRequests.Register)]
		public static void HandleRegister(object payload)
		{
			// TODO: Navigate to registration scene/flow
		}
	}
}
