using Features.GamePlay.SubFeatures.Home.Events;
using Features.GamePlay.SubFeatures.Home.Infrastructure;
using Features.GamePlay.SubFeatures.Home.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Home.Model;
using Features.GamePlay.SubFeatures.Home.Requests;

namespace Features.GamePlay.SubFeatures.Home.Controller
{
	/// <summary>
	/// Controller for Home.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class HomeController
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
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(HomeParentSignals signals)
		{
			HomeState.ParentSignals = signals;
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(HomeRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(HomeEvents.Echoed, payload);
			HomeState.ParentSignals?.OnEchoed?.Invoke(payload);
		}
	}
}
