using Features.GamePlay.SubFeatures.MyLog.Events;
using Features.GamePlay.SubFeatures.MyLog.Infrastructure;
using Features.GamePlay.SubFeatures.MyLog.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.MyLog.Model;
using Features.GamePlay.SubFeatures.MyLog.Requests;

namespace Features.GamePlay.SubFeatures.MyLog.Controller
{
	/// <summary>
	/// Controller for MyLog.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class MyLogController
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
		public static void SetParentSignals(MyLogParentSignals signals)
		{
			MyLogState.ParentSignals = signals;
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(MyLogRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(MyLogEvents.Echoed, payload);
			MyLogState.ParentSignals?.OnEchoed?.Invoke(payload);
		}
	}
}
