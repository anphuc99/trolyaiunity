using Features.CharacterInfo.Events;
using Features.CharacterInfo.Infrastructure;
using Features.CharacterInfo.Infrastructure.Attributes;
using Features.CharacterInfo.Requests;

namespace Features.CharacterInfo.Controller
{
	/// <summary>
	/// Controller for CharacterInfo.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.CharacterInfoGameplay)]
	public static class CharacterInfoController
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
		[Request(CharacterInfoRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(CharacterInfoEvents.Echoed, payload);
		}
	}
}
