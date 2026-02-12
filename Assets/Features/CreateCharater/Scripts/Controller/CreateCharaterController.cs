using Features.CreateCharater.Events;
using Features.CreateCharater.Infrastructure;
using Features.CreateCharater.Infrastructure.Attributes;
using Features.CreateCharater.Requests;

namespace Features.CreateCharater.Controller
{
	/// <summary>
	/// Controller for CreateCharater.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.CreateCharaterGameplay)]
	public static class CreateCharaterController
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
		[Request(CreateCharaterRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(CreateCharaterEvents.Echoed, payload);
		}
	}
}
