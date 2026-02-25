using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Character.Events;
using Features.GamePlay.SubFeatures.Character.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Character.Model;
using Features.GamePlay.SubFeatures.Character.Requests;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Character.View
{
	/// <summary>
	/// View for Character.
	/// </summary>
	public sealed class CharacterView : BaseView
	{
		[SerializeField]
		private GameObject _container;

		[SerializeField]
		private CharacterItem _characterItemPrefab;

		private readonly List<CharacterItem> _spawnedItems = new List<CharacterItem>();

		/// <summary>
		/// Shows Character view and requests character list.
		/// </summary>
		/// <param name="payload">Unused payload.</param>

		[OnEvent(CharacterEvents.Installed)]
		private void OnInstalled(object payload)
		{
			gameObject.SetActive(true);
			SendRequest(CharacterRequests.LoadCharacters);
		}

		/// <summary>
		/// Renders character list into character item views.
		/// </summary>
		/// <param name="payload">List payload from controller.</param>
		[OnEvent(CharacterEvents.CharactersLoaded)]
		private void OnCharactersLoaded(object payload)
		{
			var characters = payload as List<CharacterListItemPayload>;
			RenderCharacters(characters);
		}

		/// <summary>
		/// Clears all character item instances and hides this view.
		/// </summary>
		/// <param name="payload">Unused payload.</param>

		[OnEvent(CharacterEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			ClearItems();
			gameObject.SetActive(false);
		}

		private void RenderCharacters(List<CharacterListItemPayload> characters)
		{
			ClearItems();

			if (_container == null || _characterItemPrefab == null || characters == null)
			{
				return;
			}

			for (var i = 0; i < characters.Count; i++)
			{
				var itemData = characters[i];
				if (itemData == null || string.IsNullOrWhiteSpace(itemData.Name))
				{
					continue;
				}

				var item = Instantiate(_characterItemPrefab, _container.transform);
				item.gameObject.SetActive(true);
				var characterName = itemData.Name.Trim();
				var avatar = SendRequest<Sprite>(CharacterRequests.GetCharacterAvatar, characterName);
				item.Initialize(characterName, avatar, null);
				_spawnedItems.Add(item);
			}
		}

		private void ClearItems()
		{
			for (var i = 0; i < _spawnedItems.Count; i++)
			{
				if (_spawnedItems[i] != null)
				{
					Destroy(_spawnedItems[i].gameObject);
				}
			}

			_spawnedItems.Clear();

			if (_container == null)
			{
				return;
			}

			var existingItems = _container.GetComponentsInChildren<CharacterItem>(true);
			for (var i = 0; i < existingItems.Length; i++)
			{
				if (existingItems[i] == null)
				{
					continue;
				}

				Destroy(existingItems[i].gameObject);
			}
		}
	}
}
