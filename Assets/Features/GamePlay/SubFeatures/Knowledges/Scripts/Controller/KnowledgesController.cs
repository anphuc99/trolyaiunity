using Features.GamePlay.SubFeatures.Knowledges.Events;
using Features.GamePlay.SubFeatures.Knowledges.Infrastructure;
using Features.GamePlay.SubFeatures.Knowledges.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Knowledges.Model;
using Features.GamePlay.SubFeatures.Knowledges.Requests;

namespace Features.GamePlay.SubFeatures.Knowledges.Controller
{
	/// <summary>
	/// Controller for Knowledges.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class KnowledgesController
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
		/// Installs the Knowledges subfeature.
		/// </summary>
		public static void Install()
		{
			EventBus.Publish(KnowledgesEvents.Installed, null);
		}

		/// <summary>
		/// Uninstalls the Knowledges subfeature.
		/// </summary>
		public static void Uninstall()
		{
			EventBus.Publish(KnowledgesEvents.Uninstalled, null);
		}

		/// <summary>
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(KnowledgesParentSignals signals)
		{
			KnowledgesState.ParentSignals = signals;
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(KnowledgesRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(KnowledgesEvents.Echoed, payload);
			KnowledgesState.ParentSignals?.OnEchoed?.Invoke(payload);
		}
	}
}
