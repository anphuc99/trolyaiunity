using System.Threading.Tasks;
using Features.GamePlay.Events;
using Features.GamePlay.Infrastructure;
using Features.GamePlay.Infrastructure.Attributes;
using Features.GamePlay.Model;
using Features.GamePlay.Requests;
using Features.GamePlay.SubFeatures.Character.Controller;
using Features.GamePlay.SubFeatures.Character.Model;
using Features.GamePlay.SubFeatures.Chat.Controller;
using Features.GamePlay.SubFeatures.Chat.Model;
using Features.GamePlay.SubFeatures.Home.Controller;
using Features.GamePlay.SubFeatures.Home.Model;
using Features.GamePlay.SubFeatures.Journal.Controller;
using Features.GamePlay.SubFeatures.Journal.Model;
using Features.GamePlay.SubFeatures.Practice.Controller;
using Features.GamePlay.SubFeatures.Practice.Model;
using Features.GamePlay.SubFeatures.Story.Controller;
using Features.GamePlay.SubFeatures.Story.Model;
using Features.GamePlay.SubFeatures.Task.Controller;
using Features.GamePlay.SubFeatures.Task.Model;

namespace Features.GamePlay.Controller
{
	/// <summary>
	/// Controller for GamePlay.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class GamePlayController
	{
		/// <summary>
		/// Called when the controller scope is entered.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerInit]
		public static async void OnEnterScope()
		{
			SetAllSubControllerSignals();
			await Task.Yield();
			InstallSubController(GamePlaySubControllerType.Home);
		}

		/// <summary>
		/// Called when the controller scope is exited.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerShutdown]
		public static void OnExitScope()
		{
			CloseCurrentSubControllerInternal();
			ClearAllSubControllerSignals();
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(GamePlayRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(GamePlayEvents.Echoed, payload);
		}

		/// <summary>
		/// Opens a subcontroller by name or enum value.
		/// Automatically closes the currently opened subcontroller first.
		/// </summary>
		/// <param name="payload">Subcontroller name or <see cref="GamePlaySubControllerType"/> value.</param>
		/// <returns>True when opened successfully; otherwise false.</returns>
		[Request(GamePlayRequests.OpenSubController)]
		public static bool HandleOpenSubController(object payload)
		{
			if (!TryResolveSubController(payload, out var target))
			{
				return false;
			}

			if (GamePlayState.CurrentSubController == target)
			{
				return true;
			}

			CloseCurrentSubControllerInternal();
			InstallSubController(target);
			GamePlayState.CurrentSubController = target;
			EventBus.Publish(GamePlayEvents.SubControllerChanged, target.ToString());
			return true;
		}

		/// <summary>
		/// Closes the currently opened subcontroller.
		/// </summary>
		[Request(GamePlayRequests.CloseCurrentSubController)]
		public static void HandleCloseCurrentSubController()
		{
			CloseCurrentSubControllerInternal();
		}

		/// <summary>
		/// Gets the currently active subcontroller name.
		/// </summary>
		/// <returns>Current subcontroller name, or null if none is active.</returns>
		[Request(GamePlayRequests.GetCurrentSubController)]
		public static string HandleGetCurrentSubController()
		{
			return GamePlayState.CurrentSubController?.ToString();
		}

		/// <summary>
		/// Installs a subcontroller by binding parent signals.
		/// </summary>
		/// <param name="subControllerType">Subcontroller to install.</param>
		private static void InstallSubController(GamePlaySubControllerType subControllerType)
		{
			switch (subControllerType)
			{
				case GamePlaySubControllerType.Home:
					HomeController.Install();
					break;
				case GamePlaySubControllerType.Character:
					CharacterController.Install();
					break;
				case GamePlaySubControllerType.Chat:
					ChatController.Install();
					break;
				case GamePlaySubControllerType.Journal:
					JournalController.Install();
					break;
				case GamePlaySubControllerType.Practice:
					PracticeController.Install();
					break;
				case GamePlaySubControllerType.Story:
					StoryController.Install();
					break;
				case GamePlaySubControllerType.Task:
					TaskController.Install();
					break;
			}
		}

		/// <summary>
		/// Sets parent signals for all subcontrollers when entering gameplay scope.
		/// </summary>
		private static void SetAllSubControllerSignals()
		{
			HomeController.SetParentSignals(new HomeParentSignals { OnEchoed = OnSubControllerEchoed });
			CharacterController.SetParentSignals(new CharacterParentSignals { OnEchoed = OnSubControllerEchoed });
			ChatController.SetParentSignals(new ChatParentSignals { OnEchoed = OnSubControllerEchoed });
			JournalController.SetParentSignals(new JournalParentSignals { OnEchoed = OnSubControllerEchoed });
			PracticeController.SetParentSignals(new PracticeParentSignals { OnEchoed = OnSubControllerEchoed });
			StoryController.SetParentSignals(new StoryParentSignals { OnEchoed = OnSubControllerEchoed });
			TaskController.SetParentSignals(new TaskParentSignals { OnEchoed = OnSubControllerEchoed });
		}

		/// <summary>
		/// Clears parent signals for all subcontrollers when exiting gameplay scope.
		/// </summary>
		private static void ClearAllSubControllerSignals()
		{
			HomeController.SetParentSignals(null);
			CharacterController.SetParentSignals(null);
			ChatController.SetParentSignals(null);
			JournalController.SetParentSignals(null);
			PracticeController.SetParentSignals(null);
			StoryController.SetParentSignals(null);
			TaskController.SetParentSignals(null);
		}

		/// <summary>
		/// Uninstalls a subcontroller by unbinding parent signals.
		/// </summary>
		/// <param name="subControllerType">Subcontroller to uninstall.</param>
		private static void UninstallSubController(GamePlaySubControllerType subControllerType)
		{
			switch (subControllerType)
			{
				case GamePlaySubControllerType.Home:
					HomeController.Uninstall();
					break;
				case GamePlaySubControllerType.Character:
					CharacterController.Uninstall();
					break;
				case GamePlaySubControllerType.Chat:
					ChatController.Uninstall();
					break;
				case GamePlaySubControllerType.Journal:
					JournalController.Uninstall();
					break;
				case GamePlaySubControllerType.Practice:
					PracticeController.Uninstall();
					break;
				case GamePlaySubControllerType.Story:
					StoryController.Uninstall();
					break;
				case GamePlaySubControllerType.Task:
					TaskController.Uninstall();
					break;
			}
		}

		/// <summary>
		/// Closes and uninstalls the current subcontroller if one is active.
		/// </summary>
		private static void CloseCurrentSubControllerInternal()
		{
			if (!GamePlayState.CurrentSubController.HasValue)
			{
				return;
			}

			UninstallSubController(GamePlayState.CurrentSubController.Value);
			GamePlayState.CurrentSubController = null;
			EventBus.Publish(GamePlayEvents.SubControllerChanged, null);
		}

		/// <summary>
		/// Tries to resolve a subcontroller value from request payload.
		/// </summary>
		/// <param name="payload">Incoming request payload.</param>
		/// <param name="subControllerType">Resolved subcontroller type.</param>
		/// <returns>True if payload maps to a valid subcontroller.</returns>
		private static bool TryResolveSubController(object payload, out GamePlaySubControllerType subControllerType)
		{
			if (payload is GamePlaySubControllerType typed)
			{
				subControllerType = typed;
				return true;
			}

			if (payload is string text && !string.IsNullOrWhiteSpace(text))
			{
				return System.Enum.TryParse(text.Trim(), ignoreCase: true, out subControllerType);
			}

			subControllerType = default;
			return false;
		}

		/// <summary>
		/// Handles echo signals from installed subcontrollers.
		/// </summary>
		/// <param name="payload">Signal payload.</param>
		private static void OnSubControllerEchoed(object payload)
		{
			EventBus.Publish(GamePlayEvents.Echoed, payload);
		}
	}
}
