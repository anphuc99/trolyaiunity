using Features.GamePlay.SubFeatures.Character.Events;
using Features.GamePlay.SubFeatures.Character.Infrastructure;
using Features.GamePlay.SubFeatures.Character.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Character.Model;
using Features.GamePlay.SubFeatures.Character.Requests;

namespace Features.GamePlay.SubFeatures.Character.Controller
{
	/// <summary>
	/// Controller for Character.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class CharacterController
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
			EventBus.Publish(CharacterEvents.Installed, null);
		}

		/// <summary>
		/// Uninstalls the subcontroller and clears bound parent signals.
		/// </summary>
		public static void Uninstall()
		{
			EventBus.Publish(CharacterEvents.Uninstalled, null);
		}

		/// <summary>
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(CharacterParentSignals signals)
		{
			CharacterState.ParentSignals = signals;
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(CharacterRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(CharacterEvents.Echoed, payload);
			CharacterState.ParentSignals?.OnEchoed?.Invoke(payload);
		}
	}
}
