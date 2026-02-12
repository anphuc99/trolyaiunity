using Features.GamePlay.Events;
using Features.GamePlay.Infrastructure;
using Features.GamePlay.Infrastructure.Attributes;
using Features.GamePlay.Requests;

namespace Features.GamePlay.Controller
{
	/// <summary>
	/// Controller for GamePlay.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class GamePlayController
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
		[Request(GamePlayRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(GamePlayEvents.Echoed, payload);
		}
	}
}
