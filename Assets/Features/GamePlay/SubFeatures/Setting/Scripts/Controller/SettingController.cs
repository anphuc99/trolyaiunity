using Features.GamePlay.SubFeatures.Setting.Events;
using Features.GamePlay.SubFeatures.Setting.Infrastructure;
using Features.GamePlay.SubFeatures.Setting.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Setting.Model;
using Features.GamePlay.SubFeatures.Setting.Requests;

namespace Features.GamePlay.SubFeatures.Setting.Controller
{
	/// <summary>
	/// Controller for Setting.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class SettingController
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
		public static void SetParentSignals(SettingParentSignals signals)
		{
			SettingState.ParentSignals = signals;
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(SettingRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(SettingEvents.Echoed, payload);
			SettingState.ParentSignals?.OnEchoed?.Invoke(payload);
		}
	}
}
