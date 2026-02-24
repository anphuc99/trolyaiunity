using Features.GamePlay.SubFeatures.Story.Events;
using Features.GamePlay.SubFeatures.Story.Infrastructure;
using Features.GamePlay.SubFeatures.Story.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Story.Model;
using Features.GamePlay.SubFeatures.Story.Requests;

namespace Features.GamePlay.SubFeatures.Story.Controller
{
	/// <summary>
	/// Controller for Story.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class StoryController
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
		}

		/// <summary>
		/// Uninstalls the subcontroller and clears bound parent signals.
		/// </summary>
		public static void Uninstall()
		{
		}

		/// <summary>
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(StoryParentSignals signals)
		{
			StoryState.ParentSignals = signals;
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(StoryRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(StoryEvents.Echoed, payload);
			StoryState.ParentSignals?.OnEchoed?.Invoke(payload);
		}
	}
}
