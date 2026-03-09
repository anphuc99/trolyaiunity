using Features.GamePlay.SubFeatures.Subjects.Events;
using Features.GamePlay.SubFeatures.Subjects.Infrastructure;
using Features.GamePlay.SubFeatures.Subjects.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Subjects.Model;
using Features.GamePlay.SubFeatures.Subjects.Requests;

namespace Features.GamePlay.SubFeatures.Subjects.Controller
{
	/// <summary>
	/// Controller for Subjects.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class SubjectsController
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
		public static void SetParentSignals(SubjectsParentSignals signals)
		{
			SubjectsState.ParentSignals = signals;
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(SubjectsRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(SubjectsEvents.Echoed, payload);
			SubjectsState.ParentSignals?.OnEchoed?.Invoke(payload);
		}
	}
}
