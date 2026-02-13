using Features.GamePlay.SubFeatures.Homework.Events;
using Features.GamePlay.SubFeatures.Homework.Infrastructure;
using Features.GamePlay.SubFeatures.Homework.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Homework.Model;
using Features.GamePlay.SubFeatures.Homework.Requests;

namespace Features.GamePlay.SubFeatures.Homework.Controller
{
	/// <summary>
	/// Controller for Homework.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class HomeworkController
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
		public static void SetParentSignals(HomeworkParentSignals signals)
		{
			HomeworkState.ParentSignals = signals;
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(HomeworkRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(HomeworkEvents.Echoed, payload);
			HomeworkState.ParentSignals?.OnEchoed?.Invoke(payload);
		}
	}
}
