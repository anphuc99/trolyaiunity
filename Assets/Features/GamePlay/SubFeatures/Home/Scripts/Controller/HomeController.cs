using Features.GamePlay.SubFeatures.Home.Events;
using Features.GamePlay.SubFeatures.Home.Infrastructure;
using Features.GamePlay.SubFeatures.Home.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Home.Model;
using Features.GamePlay.SubFeatures.Home.Requests;

namespace Features.GamePlay.SubFeatures.Home.Controller
{
	/// <summary>
	/// Controller for Home.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class HomeController
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
			EventBus.Publish(HomeEvents.Installed, null);
		}

		/// <summary>
		/// Uninstalls the subcontroller and clears bound parent signals.
		/// </summary>
		public static void Uninstall()
		{
			EventBus.Publish(HomeEvents.Uninstalled, null);
		}

		/// <summary>
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(HomeParentSignals signals)
		{
			HomeState.ParentSignals = signals;
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(HomeRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(HomeEvents.Echoed, payload);
			HomeState.ParentSignals?.OnEchoed?.Invoke(payload);
		}

		[Request(HomeRequests.OpenJournal)]
		public static void HandleOpenJournal(object payload)
		{
			HomeState.ParentSignals?.OpenJournal?.Invoke();
		}

		[Request(HomeRequests.OpenStory)]
		public static void HandleOpenStory()
		{
			HomeState.ParentSignals?.OpenStory?.Invoke();
		}

		[Request(HomeRequests.OpenCreateCharacter)]
		public static void HandleOpenCreateCharacter()
		{
			HomeState.ParentSignals?.OpenCreateCharacter?.Invoke();
		}

		[Request(HomeRequests.OpenCharacter)]
		public static void HandleOpenCharacter()
		{
			HomeState.ParentSignals?.OpenCharacter?.Invoke();
		}

		[Request(HomeRequests.OpenPractice)]
		public static void HandleOpenPractice()
		{
			HomeState.ParentSignals?.OpenPractice?.Invoke();
		}

		[Request(HomeRequests.OpenTask)]
		public static void HandleOpenTask()
		{
			HomeState.ParentSignals?.OpenTask?.Invoke();
		}
	}
}
