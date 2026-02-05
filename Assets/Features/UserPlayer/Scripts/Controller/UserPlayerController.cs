using Features.UserPlayer.Events;
using Features.UserPlayer.Infrastructure;
using Features.UserPlayer.Infrastructure.Attributes;
using Features.UserPlayer.Requests;

namespace Features.UserPlayer.Controller
{
	/// <summary>
	/// Controller for UserPlayer.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.UserPlayerGameplay)]
	public static class UserPlayerController
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
		[Request(UserPlayerRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(UserPlayerEvents.Echoed, payload);
		}
	}
}
