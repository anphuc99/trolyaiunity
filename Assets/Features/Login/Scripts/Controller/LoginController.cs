using Features.Login.Events;
using Features.Login.Infrastructure;
using Features.Login.Infrastructure.Attributes;
using Features.Login.Requests;

namespace Features.Login.Controller
{
	/// <summary>
	/// Controller for Login.
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
		/// Sample request handler that echoes payload to a view event.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(LoginRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(LoginEvents.Echoed, payload);
		}
	}
}
