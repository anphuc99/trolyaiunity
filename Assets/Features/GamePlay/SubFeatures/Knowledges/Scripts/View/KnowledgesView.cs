using System.Collections.Generic;
using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Knowledges.Events;
using Features.GamePlay.SubFeatures.Knowledges.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Knowledges.Model;
using Features.GamePlay.SubFeatures.Knowledges.Requests;
using UnityEngine;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.Knowledges.View
{
	/// <summary>
	/// View for Knowledges subfeature.
	/// Displays a list of knowledge items for the selected subject.
	/// </summary>
	public sealed class KnowledgesView : BaseView
	{
		[SerializeField]
		private KnowledgesItemView _itemPrefab;

		[SerializeField]
		private Transform _contentParent;

		[SerializeField]
		private Button _learnButton;

		private readonly List<KnowledgesItemView> _spawnedItems = new List<KnowledgesItemView>();

		/// <summary>
		/// Wires button click listeners when the view is enabled.
		/// </summary>
		protected override void OnEnabled()
		{
			if (_learnButton != null)
			{
				_learnButton.onClick.RemoveListener(OnLearnButtonClicked);
				_learnButton.onClick.AddListener(OnLearnButtonClicked);
			}
		}

		/// <summary>
		/// Removes button click listeners when the view is disabled.
		/// </summary>
		protected override void OnDisabled()
		{
			if (_learnButton != null)
			{
				_learnButton.onClick.RemoveListener(OnLearnButtonClicked);
			}
		}

		/// <summary>
		/// Shows Knowledges view and requests loading the knowledge list.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(KnowledgesEvents.Installed)]
		private void OnInstalled(object payload)
		{
			gameObject.SetActive(true);
			SendRequest(KnowledgesRequests.LoadKnowledges);
		}

		/// <summary>
		/// Hides the Knowledges view and cleans up spawned items.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(KnowledgesEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			ClearItems();
			gameObject.SetActive(false);
		}

		/// <summary>
		/// Renders the loaded knowledges into the scrollable list.
		/// </summary>
		/// <param name="payload">List of KnowledgeItemPayload from the controller.</param>
		[OnEvent(KnowledgesEvents.KnowledgesLoaded)]
		private void OnKnowledgesLoaded(object payload)
		{
			var knowledges = payload as List<KnowledgeItemPayload>;
			RenderKnowledges(knowledges);
		}

		/// <summary>
		/// Handles knowledges load failure by logging the error.
		/// </summary>
		/// <param name="payload">KnowledgesErrorPayload with the error message.</param>
		[OnEvent(KnowledgesEvents.KnowledgesLoadFailed)]
		private void OnKnowledgesLoadFailed(object payload)
		{
			var error = payload as KnowledgesErrorPayload;
			if (error != null)
			{
				Debug.LogError("[KnowledgesView] " + error.Message);
			}
		}

		/// <summary>
		/// Renders knowledge items into the container, clearing previous items first.
		/// </summary>
		/// <param name="knowledges">List of knowledge data payloads.</param>
		private void RenderKnowledges(List<KnowledgeItemPayload> knowledges)
		{
			ClearItems();

			if (_contentParent == null || _itemPrefab == null || knowledges == null)
			{
				return;
			}

			for (var i = 0; i < knowledges.Count; i++)
			{
				var knowledgeData = knowledges[i];
				if (knowledgeData == null || string.IsNullOrWhiteSpace(knowledgeData.Name))
				{
					continue;
				}

				var item = Instantiate(_itemPrefab, _contentParent);
				item.gameObject.SetActive(true);
				item.Initialize(knowledgeData);
				_spawnedItems.Add(item);
			}
		}

		/// <summary>
		/// Handles learn button click by sending start learning request.
		/// </summary>
		private void OnLearnButtonClicked()
		{
			SendRequest(KnowledgesRequests.StartLearning);
		}

		/// <summary>
		/// Clears all spawned knowledge item views.
		/// </summary>
		private void ClearItems()
		{
			for (var i = 0; i < _spawnedItems.Count; i++)
			{
				var item = _spawnedItems[i];
				if (item != null && item.gameObject != null)
				{
					Destroy(item.gameObject);
				}
			}

			_spawnedItems.Clear();
		}
	}
}
