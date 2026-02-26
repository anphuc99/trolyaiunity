using Features.CharacterInfo.Events;
using Features.CharacterInfo.Infrastructure;
using Features.CharacterInfo.Infrastructure.Attributes;
using Features.CharacterInfo.Requests;
using CoreEvents = Core.Infrastructure.Events;
using Core.Infrastructure.Network;
using Core.Infrastructure.Scenes;
using Core.Infrastructure.State;
using Share.Model;
using UnityEngine;

namespace Features.CharacterInfo.Controller
{
	/// <summary>
	/// Controller for CharacterInfo.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.CharacterInfoGameplay)]
	public static class CharacterInfoController
	{
		private const string SelectedCharacterGlobalKey = "global.character.selected.info";
		private const string DeletedCharacterGlobalKey = "global.character.deleted.notice";
		private const string EditedCharacterGlobalKey = "global.character.edited.notice";

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

		/// <summary>
		/// Closes CharacterInfo scope.
		/// </summary>
		[Request(CharacterInfoRequests.CloseScope)]
		public static void HandleCloseScope()
		{
			LoadScene.UnloadByScope(Core.Infrastructure.Attributes.ControllerScopeKey.CharacterInfoGameplay);
		}

		/// <summary>
		/// Removes selected character from server and closes CharacterInfo scope.
		/// </summary>
		[Request(CharacterInfoRequests.RemoveSelectedCharacter)]
		public static async void HandleRemoveSelectedCharacter()
		{
			if (!GlobalVariables.TryGet<SelectedCharacterInfo>(SelectedCharacterGlobalKey, out var selectedCharacter)
				|| selectedCharacter == null
				|| selectedCharacter.Id <= 0)
			{
				return;
			}

			var endpoint = $"{NetworkEndpoints.Characters}/{selectedCharacter.Id}";
			var responseJson = await HttpClient.DeleteTaskAsync(endpoint);
			if (responseJson == null)
			{
				Debug.LogError("[CharacterInfoController] Failed to delete selected character.");
				return;
			}

			SetDeletedCharacterNotice(selectedCharacter);

			LoadScene.UnloadByScope(Core.Infrastructure.Attributes.ControllerScopeKey.CharacterInfoGameplay);
		}

		[Request(CharacterInfoRequests.OpenEditCharacter)]
		public static void HandleOpenEditCharacter()
		{
			GlobalVariables.Remove(EditedCharacterGlobalKey);
			LoadScene.ByScope(Core.Infrastructure.Attributes.ControllerScopeKey.EditCharacterGameplay, UnityEngine.SceneManagement.LoadSceneMode.Additive);
		}

		[Core.Infrastructure.Attributes.OnGlobalScopeChanged]
		private static void HandleScopeChanged(CoreEvents.ScopeChangedPayload payload)
		{
			if (payload == null
				|| payload.IsOpened
				|| payload.ScopeKey != Core.Infrastructure.Attributes.ControllerScopeKey.EditCharacterGameplay)
			{
				return;
			}

			if (!GlobalVariables.TryGet<EditedCharacterNotice>(EditedCharacterGlobalKey, out var notice)
				|| notice == null
				|| notice.Character == null)
			{
				return;
			}

			SetSelectedCharacterInfo(notice.Character);
			PublishSelectedCharacter();
		}

		private static void SetDeletedCharacterNotice(SelectedCharacterInfo selectedCharacter)
		{
			if (selectedCharacter == null)
			{
				return;
			}

			GlobalVariables.Set(DeletedCharacterGlobalKey, new DeletedCharacterNotice
			{
				CharacterId = selectedCharacter.Id,
				CharacterName = selectedCharacter.Name,
			});
		}

		private static void SetSelectedCharacterInfo(SelectedCharacterInfo selectedCharacter)
		{
			GlobalVariables.Set(SelectedCharacterGlobalKey, selectedCharacter);
		}

		private static void PublishSelectedCharacter()
		{
			GlobalVariables.TryGet<SelectedCharacterInfo>(SelectedCharacterGlobalKey, out var selectedCharacter);
			EventBus.Publish(CharacterInfoEvents.SelectedCharacterLoaded, selectedCharacter);
		}
	}
}
