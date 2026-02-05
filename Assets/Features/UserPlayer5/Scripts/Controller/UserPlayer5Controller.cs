using Features.UserPlayer5.Events;
using Features.UserPlayer5.Infrastructure;
using Features.UserPlayer5.Infrastructure.Attributes;
using Features.UserPlayer5.Requests;

namespace Features.UserPlayer5.Controller
{
	/// <summary>
	/// Controller for UserPlayer5.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.UserPlayer5Gameplay)]
	public static class UserPlayer5Controller
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
		[Request(UserPlayer5Requests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(UserPlayer5Events.Echoed, payload);
		}
	}
}
