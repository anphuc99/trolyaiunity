using Features.CharacterInfo.Events;
using Features.CharacterInfo.Infrastructure;
using Features.CharacterInfo.Infrastructure.Attributes;
using Features.CharacterInfo.Requests;
using Core.Infrastructure.State;
using Share.Model;

namespace Features.CharacterInfo.Controller
{
	/// <summary>
	/// Controller for CharacterInfo.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.CharacterInfoGameplay)]
	public static class CharacterInfoController
	{
		private const string SelectedCharacterGlobalKey = "global.character.selected.info";

		/// <summary>
		/// Called when the controller scope is entered.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerInit]
		public static void OnEnterScope()
		{
			PublishSelectedCharacter();
		}

		/// <summary>
		/// Called when the controller scope is exited.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerShutdown]
		public static void OnExitScope()
		{
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(CharacterInfoRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(CharacterInfoEvents.Echoed, payload);
		}

		/// <summary>
		/// Loads selected character info from global variables and publishes to view.
		/// </summary>
		[Request(CharacterInfoRequests.LoadSelectedCharacter)]
		public static void HandleLoadSelectedCharacter()
		{
			PublishSelectedCharacter();
		}

		private static void PublishSelectedCharacter()
		{
			GlobalVariables.TryGet<SelectedCharacterInfo>(SelectedCharacterGlobalKey, out var selectedCharacter);
			EventBus.Publish(CharacterInfoEvents.SelectedCharacterLoaded, selectedCharacter);
		}
	}
}
