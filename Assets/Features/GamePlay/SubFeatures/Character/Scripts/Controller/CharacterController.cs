using Features.GamePlay.SubFeatures.Character.Events;
using Features.GamePlay.SubFeatures.Character.Infrastructure;
using Features.GamePlay.SubFeatures.Character.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Character.Model;
using Features.GamePlay.SubFeatures.Character.Requests;
using System;
using System.Collections.Generic;
using UnityEngine;

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
			CharacterState.CachedCharacters = new List<CharacterListItemPayload>();
		}

		/// <summary>
		/// Installs the subcontroller.
		/// </summary>
		public static void Install()
		{
			EventBus.Publish(CharacterEvents.Installed, null);
			HandleLoadCharacters();
		}

		/// <summary>
		/// Uninstalls the subcontroller and clears bound parent signals.
		/// </summary>
		public static void Uninstall()
		{
			CharacterState.CachedCharacters = new List<CharacterListItemPayload>();
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

		/// <summary>
		/// Loads character list from parent scope cache and publishes to view.
		/// </summary>
		[Request(CharacterRequests.LoadCharacters)]
		public static void HandleLoadCharacters()
		{
			var names = CharacterState.ParentSignals?.GetCharacterNames?.Invoke();
			var payload = new List<CharacterListItemPayload>();
			if (names != null)
			{
				for (var i = 0; i < names.Count; i++)
				{
					var name = names[i];
					if (string.IsNullOrWhiteSpace(name))
					{
						continue;
					}

					payload.Add(new CharacterListItemPayload
					{
						Name = name.Trim()
					});
				}
			}

			CharacterState.CachedCharacters = payload;
			EventBus.Publish(CharacterEvents.CharactersLoaded, payload);
		}

		/// <summary>
		/// Gets character avatar sprite from parent scope cache.
		/// </summary>
		/// <param name="payload">Character name payload.</param>
		/// <returns>Avatar sprite when available; otherwise null.</returns>
		[Request(CharacterRequests.GetCharacterAvatar)]
		public static Sprite HandleGetCharacterAvatar(object payload)
		{
			if (payload is not string characterName || string.IsNullOrWhiteSpace(characterName))
			{
				return null;
			}

			return CharacterState.ParentSignals?.GetCharacterAvatar?.Invoke(characterName.Trim());
		}
	}
}
