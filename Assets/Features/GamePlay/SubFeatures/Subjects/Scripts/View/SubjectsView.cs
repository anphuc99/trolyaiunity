using System.Collections.Generic;
using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Subjects.Events;
using Features.GamePlay.SubFeatures.Subjects.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Subjects.Model;
using Features.GamePlay.SubFeatures.Subjects.Requests;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Subjects.View
{
	/// <summary>
	/// View for the Subjects subfeature.
	/// Displays a scrollable list of subjects and handles user interactions.
	/// </summary>
	public sealed class SubjectsView : BaseView
	{
		[SerializeField]
		private SubjectsItemView _itemViewPrefab;

		[SerializeField]
		private Transform _itemsContainer;

		private readonly List<SubjectsItemView> _spawnedItems = new List<SubjectsItemView>();

		/// <summary>
		/// Shows Subjects view and requests subject list loading.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(SubjectsEvents.Installed)]
		private void OnInstalled(object payload)
		{
			gameObject.SetActive(true);
			SendRequest(SubjectsRequests.LoadSubjects);
		}

		/// <summary>
		/// Hides Subjects view and cleans up spawned items.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(SubjectsEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			ClearItems();
			gameObject.SetActive(false);
		}

		/// <summary>
		/// Renders the loaded subjects into the scrollable list.
		/// </summary>
		/// <param name="payload">List of SubjectItemPayload from the controller.</param>
		[OnEvent(SubjectsEvents.SubjectsLoaded)]
		private void OnSubjectsLoaded(object payload)
		{
			var subjects = payload as List<SubjectItemPayload>;
			RenderSubjects(subjects);
		}

		/// <summary>
		/// Handles subject load failure by logging the error.
		/// </summary>
		/// <param name="payload">SubjectsErrorPayload with the error message.</param>
		[OnEvent(SubjectsEvents.SubjectsLoadFailed)]
		private void OnSubjectsLoadFailed(object payload)
		{
			var error = payload as SubjectsErrorPayload;
			if (error != null)
			{
				Debug.LogError("[SubjectsView] " + error.Message);
			}
		}

		/// <summary>
		/// Renders subject items into the container, clearing previous items first.
		/// </summary>
		/// <param name="subjects">List of subject data payloads.</param>
		private void RenderSubjects(List<SubjectItemPayload> subjects)
		{
			ClearItems();

			if (_itemsContainer == null || _itemViewPrefab == null || subjects == null)
			{
				return;
			}

			for (var i = 0; i < subjects.Count; i++)
			{
				var subjectData = subjects[i];
				if (subjectData == null || string.IsNullOrWhiteSpace(subjectData.Name))
				{
					continue;
				}

				var item = Instantiate(_itemViewPrefab, _itemsContainer);
				item.gameObject.SetActive(true);
				item.Initialize(subjectData, OnSubjectItemClicked);
				_spawnedItems.Add(item);
			}
		}

		/// <summary>
		/// Handles a subject item click by sending a select request to the controller.
		/// </summary>
		/// <param name="subject">The selected subject data.</param>
		private void OnSubjectItemClicked(SubjectItemPayload subject)
		{
			if (subject == null || subject.Id <= 0)
			{
				return;
			}

			SendRequest(SubjectsRequests.SelectSubject, subject);
		}

		/// <summary>
		/// Clears all spawned subject item views.
		/// </summary>
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

			if (_itemsContainer == null)
			{
				return;
			}

			var existingItems = _itemsContainer.GetComponentsInChildren<SubjectsItemView>(true);
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
