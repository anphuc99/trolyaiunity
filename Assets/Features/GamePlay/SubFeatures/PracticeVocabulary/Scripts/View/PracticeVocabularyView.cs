using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.PracticeVocabulary.Events;
using Features.GamePlay.SubFeatures.PracticeVocabulary.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.PracticeVocabulary.Model;
using Features.GamePlay.SubFeatures.PracticeVocabulary.Requests;
using System.Collections.Generic;
using UnityEngine;
using Share.Components;

namespace Features.GamePlay.SubFeatures.PracticeVocabulary.View
{
	/// <summary>
	/// View for PracticeVocabulary.
	/// </summary>
	public sealed class PracticeVocabularyView : BaseView
	{
		[SerializeField]
		private SharedVocabularyPopupView _vocabularyPopupView;

		private readonly List<PracticeVocabularyItemPayload> _dueVocabularies = new List<PracticeVocabularyItemPayload>();

		private int _currentDueIndex;

		/// <summary>
		/// Shows this subfeature view when controller is installed.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(PracticeVocabularyEvents.Installed)]
		private void OnInstalled(object payload)
		{
			gameObject.SetActive(true);
			EnsurePopupBinding();
			RequestDueVocabularies();
		}

		/// <summary>
		/// Hides this subfeature view when controller is uninstalled.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(PracticeVocabularyEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			ResetQueue();
			HidePopup();
			gameObject.SetActive(false);
		}

		protected override void OnEnabled()
		{
			EnsurePopupBinding();
			RequestDueVocabularies();
		}

		protected override void OnDisabled()
		{
			if (_vocabularyPopupView != null)
			{
				_vocabularyPopupView.SetReviewCallback(null);
			}
		}

		/// <summary>
		/// Handles due vocab list loaded from controller.
		/// </summary>
		/// <param name="payload">Due list payload.</param>
		[OnEvent(PracticeVocabularyEvents.DueReviewsLoaded)]
		private void OnDueReviewsLoaded(object payload)
		{
			if (payload is not PracticeVocabularyDueListResponsePayload response)
			{
				return;
			}

			ApplyDueList(response.Vocabularies);
			ShowCurrentVocabularyOrHide();
		}

		/// <summary>
		/// Handles one successful review submission.
		/// </summary>
		/// <param name="payload">Review result payload.</param>
		[OnEvent(PracticeVocabularyEvents.ReviewSubmitted)]
		private void OnReviewSubmitted(object payload)
		{
			if (_dueVocabularies.Count == 0)
			{
				RequestDueVocabularies();
				return;
			}

			_currentDueIndex += 1;
			if (_currentDueIndex < _dueVocabularies.Count)
			{
				ShowCurrentVocabularyOrHide();
				return;
			}

			// Finished current batch. Reload to check if server still has due vocabularies.
			RequestDueVocabularies();
		}

		/// <summary>
		/// Handles request failures from controller.
		/// </summary>
		/// <param name="payload">Error payload.</param>
		[OnEvent(PracticeVocabularyEvents.RequestFailed)]
		private void OnRequestFailed(object payload)
		{
			var error = payload as PracticeVocabularyErrorPayload;
			if (error != null && !string.IsNullOrWhiteSpace(error.Message))
			{
				Debug.LogWarning("[PracticeVocabularyView] " + error.Message, this);
			}

			// Re-show current item to unlock rating buttons when a submit fails.
			ShowCurrentVocabularyOrHide();
		}

		/// <summary>
		/// Handles review callback from shared popup and forwards it to controller.
		/// </summary>
		/// <param name="vocabularyId">Vocabulary id.</param>
		/// <param name="rating">FSRS rating (1..4).</param>
		private void HandleReviewRequested(string vocabularyId, int rating)
		{
			if (string.IsNullOrWhiteSpace(vocabularyId))
			{
				return;
			}

			SendRequest(PracticeVocabularyRequests.SubmitReview, new PracticeVocabularyReviewRequestPayload
			{
				VocabularyId = vocabularyId,
				Rating = rating
			});
		}

		/// <summary>
		/// Ensures popup reference and callback wiring are valid.
		/// </summary>
		private void EnsurePopupBinding()
		{
			if (_vocabularyPopupView == null)
			{
#if UNITY_2023_1_OR_NEWER
				_vocabularyPopupView = FindFirstObjectByType<SharedVocabularyPopupView>(FindObjectsInactive.Include);
#else
				_vocabularyPopupView = FindObjectOfType<SharedVocabularyPopupView>(true);
#endif
			}

			if (_vocabularyPopupView == null)
			{
				return;
			}

			_vocabularyPopupView.SetRatingButtonsVisible(true);
			_vocabularyPopupView.SetReviewCallback(HandleReviewRequested);
			_vocabularyPopupView.SetAudioPlayCallback(null);
		}

		/// <summary>
		/// Sends request to controller to fetch due vocabularies.
		/// </summary>
		private void RequestDueVocabularies()
		{
			SendRequest(PracticeVocabularyRequests.LoadDueReviews);
		}

		/// <summary>
		/// Applies fetched due list and resets traversal index.
		/// </summary>
		/// <param name="vocabularies">Fetched due vocabularies.</param>
		private void ApplyDueList(List<PracticeVocabularyItemPayload> vocabularies)
		{
			ResetQueue();

			if (vocabularies == null || vocabularies.Count == 0)
			{
				return;
			}

			for (var i = 0; i < vocabularies.Count; i += 1)
			{
				var item = vocabularies[i];
				if (item == null || string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Korean))
				{
					continue;
				}

				_dueVocabularies.Add(item);
			}
		}

		/// <summary>
		/// Shows current vocabulary item, or hides popup when queue is empty.
		/// </summary>
		private void ShowCurrentVocabularyOrHide()
		{
			var current = GetCurrentVocabulary();
			if (current == null)
			{
				HidePopup();
				return;
			}

			ShowVocabulary(current);
		}

		/// <summary>
		/// Returns current queue item by index.
		/// </summary>
		/// <returns>Current vocabulary item or null.</returns>
		private PracticeVocabularyItemPayload GetCurrentVocabulary()
		{
			if (_currentDueIndex < 0 || _currentDueIndex >= _dueVocabularies.Count)
			{
				return null;
			}

			return _dueVocabularies[_currentDueIndex];
		}

		/// <summary>
		/// Displays one vocabulary entry in shared popup.
		/// </summary>
		/// <param name="vocabulary">Vocabulary to display.</param>
		private void ShowVocabulary(PracticeVocabularyItemPayload vocabulary)
		{
			EnsurePopupBinding();
			if (_vocabularyPopupView == null || vocabulary == null)
			{
				return;
			}

			var safeWord = string.IsNullOrWhiteSpace(vocabulary.Korean) ? string.Empty : vocabulary.Korean.Trim();
			_vocabularyPopupView.ShowLoading(safeWord);
			_vocabularyPopupView.ShowResult(vocabulary.Id, safeWord, vocabulary.Pinyin, vocabulary.Vietnamese);
		}

		/// <summary>
		/// Hides popup and keeps queue data untouched.
		/// </summary>
		private void HidePopup()
		{
			if (_vocabularyPopupView != null)
			{
				_vocabularyPopupView.Hide();
			}
		}

		/// <summary>
		/// Clears current queue and resets index.
		/// </summary>
		private void ResetQueue()
		{
			_dueVocabularies.Clear();
			_currentDueIndex = 0;
		}
	}
}
