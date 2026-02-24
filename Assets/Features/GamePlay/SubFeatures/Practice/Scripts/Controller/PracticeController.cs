using Features.GamePlay.SubFeatures.Practice.Events;
using Features.GamePlay.SubFeatures.Practice.Infrastructure;
using Features.GamePlay.SubFeatures.Practice.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Practice.Model;
using Features.GamePlay.SubFeatures.Practice.Requests;

namespace Features.GamePlay.SubFeatures.Practice.Controller
{
	/// <summary>
	/// Controller for Practice.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class PracticeController
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
		/// Installs the subcontroller.
		/// </summary>
		public static void Install()
		{
			EventBus.Publish(PracticeEvents.Installed, null);
		}

		/// <summary>
		/// Uninstalls the subcontroller and clears bound parent signals.
		/// </summary>
		public static void Uninstall()
		{
			EventBus.Publish(PracticeEvents.Uninstalled, null);
		}

		/// <summary>
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(PracticeParentSignals signals)
		{
			PracticeState.ParentSignals = signals;
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(PracticeRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(PracticeEvents.Echoed, payload);
			PracticeState.ParentSignals?.OnEchoed?.Invoke(payload);
		}
	}
}
