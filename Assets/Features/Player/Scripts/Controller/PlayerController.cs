using Features.Player.Events;
using Features.Player.Infrastructure;
using Features.Player.Infrastructure.Attributes;
using Features.Player.Requests;

namespace Features.Player.Controller
{
	/// <summary>
	/// Controller for Player.
	/// </summary>
	public static class PlayerController
	{
		/// <summary>
		/// Sample request handler that echoes payload to a view event.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(PlayerRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(PlayerEvents.Echoed, payload);
		}
	}
}
