using UnityEngine;
using Features.Player.Events;
using Features.Player.Infrastructure;
using Features.Player.Infrastructure.Attributes;
using Features.Player.Requests;

namespace Features.Player.Controller
{
	/// <summary>
	/// Controller for Player.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.PlayerGameplay)]
	public static class PlayerController
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
		[Request(PlayerRequests.Echo)]
		public static int HandleEcho(object payload)
		{
			Debug.Log("[PlayerController] Handling Echo request");
			EventBus.Publish(PlayerEvents.Echoed, payload);
			return 0;
		}
	}
}
