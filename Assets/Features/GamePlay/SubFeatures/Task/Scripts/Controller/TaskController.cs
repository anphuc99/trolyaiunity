using Features.GamePlay.SubFeatures.Task.Events;
using Features.GamePlay.SubFeatures.Task.Infrastructure;
using Features.GamePlay.SubFeatures.Task.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Task.Model;
using Features.GamePlay.SubFeatures.Task.Requests;

namespace Features.GamePlay.SubFeatures.Task.Controller
{
	/// <summary>
	/// Controller for Task.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class TaskController
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
			EventBus.Publish(TaskEvents.Installed, null);
		}

		/// <summary>
		/// Uninstalls the subcontroller and clears bound parent signals.
		/// </summary>
		public static void Uninstall()
		{
			EventBus.Publish(TaskEvents.Uninstalled, null);
		}

		/// <summary>
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(TaskParentSignals signals)
		{
			TaskState.ParentSignals = signals;
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(TaskRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(TaskEvents.Echoed, payload);
			TaskState.ParentSignals?.OnEchoed?.Invoke(payload);
		}
	}
}
