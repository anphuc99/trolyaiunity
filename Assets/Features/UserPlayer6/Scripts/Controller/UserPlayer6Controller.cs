using Features.UserPlayer6.Events;
using Features.UserPlayer6.Infrastructure;
using Features.UserPlayer6.Infrastructure.Attributes;
using Features.UserPlayer6.Requests;

namespace Features.UserPlayer6.Controller
{
	/// <summary>
	/// Controller for UserPlayer6.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.UserPlayer6Gameplay)]
	public static class UserPlayer6Controller
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
		[Request(UserPlayer6Requests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(UserPlayer6Events.Echoed, payload);
		}
	}
}
