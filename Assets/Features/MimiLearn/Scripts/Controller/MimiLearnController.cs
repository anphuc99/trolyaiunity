using Features.MimiLearn.Events;
using Features.MimiLearn.Infrastructure;
using Features.MimiLearn.Infrastructure.Attributes;
using Features.MimiLearn.Requests;

namespace Features.MimiLearn.Controller
{
	/// <summary>
	/// Controller for MimiLearn.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.MimiLearnGameplay)]
	public static class MimiLearnController
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
		[Request(MimiLearnRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(MimiLearnEvents.Echoed, payload);
		}
	}
}
