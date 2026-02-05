using Features.UserPlayer2.Events;
using Features.UserPlayer2.Infrastructure;
using Features.UserPlayer2.Infrastructure.Attributes;
using Features.UserPlayer2.Requests;

namespace Features.UserPlayer2.Controller
{
	/// <summary>
	/// Controller for UserPlayer2.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.UserPlayer2Gameplay)]
	public static class UserPlayer2Controller
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
		[Request(UserPlayer2Requests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(UserPlayer2Events.Echoed, payload);
		}
	}
}
