using Features.GamePlay.SubFeatures.CreateSubjects.Events;
using Features.GamePlay.SubFeatures.CreateSubjects.Infrastructure;
using Features.GamePlay.SubFeatures.CreateSubjects.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.CreateSubjects.Model;
using Features.GamePlay.SubFeatures.CreateSubjects.Requests;

namespace Features.GamePlay.SubFeatures.CreateSubjects.Controller
{
	/// <summary>
	/// Controller for CreateSubjects.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class CreateSubjectsController
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
		public static void SetParentSignals(CreateSubjectsParentSignals signals)
		{
			CreateSubjectsState.ParentSignals = signals;
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(CreateSubjectsRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(CreateSubjectsEvents.Echoed, payload);
			CreateSubjectsState.ParentSignals?.OnEchoed?.Invoke(payload);
		}
	}
}
