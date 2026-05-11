using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.PracticeVocabulary.Events;
using Features.GamePlay.SubFeatures.PracticeVocabulary.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.PracticeVocabulary.Model;
using Features.GamePlay.SubFeatures.PracticeVocabulary.Requests;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Share.Components;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.PracticeVocabulary.View
{
	/// <summary>
	/// View for PracticeVocabulary.
	/// </summary>
	public sealed class PracticeVocabularyView : BaseView
	{
		private const string DefaultTtsTone = "neutral";

		[SerializeField]
		private SharedVocabularyPopupView _vocabularyPopupView;
		[SerializeField]
		private Button _ignoreVocabularyButton;

		[SerializeField]
		private AudioSource _characterVoiceAudioSource;

		private readonly List<PracticeVocabularyItemPayload> _dueVocabularies = new List<PracticeVocabularyItemPayload>();

		private int _currentDueIndex;
		private bool _isAudioRequestInProgress;

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
			StopAllCoroutines();
			SetAudioRequestInProgress(false);

			if (_characterVoiceAudioSource != null)
			{
				_characterVoiceAudioSource.Stop();
			}

			ResetQueue();
			HidePopup();
			gameObject.SetActive(false);
		}

		protected override void OnEnabled()
		{
			EnsurePopupBinding();
			BindIgnoreButton();
			RequestDueVocabularies();
		}

		protected override void OnDisabled()
		{
			UnbindIgnoreButton();

			if (_vocabularyPopupView != null)
			{
				_vocabularyPopupView.SetReviewCallback(null);
				_vocabularyPopupView.SetAudioPlayCallback(null);
				_vocabularyPopupView.SetGenerateExampleCallback(null);
			}

			SetAudioRequestInProgress(false);
			if (_characterVoiceAudioSource != null)
			{
				_characterVoiceAudioSource.Stop();
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
		/// Handles vocabulary ignored event — advances to next item in queue.
		/// </summary>
		/// <param name="payload">Ignored payload.</param>
		[OnEvent(PracticeVocabularyEvents.VocabularyIgnored)]
		private void OnVocabularyIgnored(object payload)
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

			RequestDueVocabularies();
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
			var wasAudioInProgress = _isAudioRequestInProgress;
			if (wasAudioInProgress)
			{
				SetAudioRequestInProgress(false);
			}

			var error = payload as PracticeVocabularyErrorPayload;
			if (error != null && !string.IsNullOrWhiteSpace(error.Message))
			{
				Debug.LogWarning("[PracticeVocabularyView] " + error.Message, this);
			}

			if (wasAudioInProgress)
			{
				return;
			}

			// Re-show current item to unlock rating buttons when a submit fails.
			ShowCurrentVocabularyOrHide();
		}

		/// <summary>
		/// Handles controller-approved vocabulary audio playback.
		/// </summary>
		/// <param name="payload">Playback payload.</param>
		[OnEvent(PracticeVocabularyEvents.VocabularyAudioPlayRequested)]
		private void OnVocabularyAudioPlayRequested(object payload)
		{
			if (_isAudioRequestInProgress)
			{
				SetAudioRequestInProgress(false);
			}

			var playback = payload as PracticeVocabularyPlayAudioPayload;
			if (playback == null || string.IsNullOrWhiteSpace(playback.Text))
			{
				return;
			}

			StartCoroutine(PlayVocabularyAudio(playback));
		}

		/// <summary>
		/// Handles the AI-generated vocabulary example sentence result from controller.
		/// Forwards the result to the vocab popup view for display.
		/// </summary>
		/// <param name="payload">Example result payload.</param>
		[OnEvent(PracticeVocabularyEvents.VocabExampleGenerated)]
		private void OnVocabExampleGenerated(object payload)
		{
			var result = payload as PracticeVocabExampleResultPayload;
			if (result == null || _vocabularyPopupView == null)
			{
				return;
			}

			_vocabularyPopupView.ShowExampleSentence(result.Sentence, result.Pinyin, result.Translation);
		}

		private IEnumerator PlayVocabularyAudio(PracticeVocabularyPlayAudioPayload playback)
		{
			if (playback.AudioClip == null)
			{
				yield break;
			}

			EnsureAudioSource();
			if (_characterVoiceAudioSource == null)
			{
				yield break;
			}

			_characterVoiceAudioSource.Stop();
			_characterVoiceAudioSource.clip = playback.AudioClip;
			_characterVoiceAudioSource.Play();

			while (_characterVoiceAudioSource.isPlaying)
			{
				yield return null;
			}
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
		/// Handles speaker button clicks from vocabulary popup.
		/// </summary>
		/// <param name="word">Vocabulary word to synthesize.</param>
		/// <param name="characterName">Selected character name from dropdown.</param>
		/// <param name="forceReload">Whether to force reload the TTS audio.</param>
		private void HandleVocabularyAudioPlayRequested(string word, string characterName, bool forceReload)
		{
			if (string.IsNullOrWhiteSpace(word))
			{
				return;
			}

			var selectedCharacterName = ResolveVocabularyAudioCharacterName(characterName);
			if (string.IsNullOrWhiteSpace(selectedCharacterName))
			{
				Debug.LogWarning("[PracticeVocabularyView] Cannot play vocabulary audio because no character is available.", this);
				return;
			}

			SetAudioRequestInProgress(true);

			SendRequest(PracticeVocabularyRequests.PlayVocabularyAudio, new PracticeVocabularyPlayAudioRequestPayload
			{
				CharacterName = selectedCharacterName,
				Text = word.Trim(),
				Tone = DefaultTtsTone,
				ForceReload = forceReload
			});
		}

		/// <summary>
		/// Callback from vocab popup when the Example Sentences button is clicked.
		/// Triggers a request to generate a story-relevant example sentence.
		/// </summary>
		/// <param name="word">The vocabulary word to generate an example for.</param>
		private void HandleGenerateVocabExample(string word)
		{
			if (string.IsNullOrWhiteSpace(word))
			{
				return;
			}

			SendRequest(PracticeVocabularyRequests.GenerateVocabExample, new PracticeVocabExampleRequestPayload
			{
				Word = word.Trim()
			});
		}

		/// <summary>
		/// Callback from vocab popup when the Next button is clicked.
		/// Submits a default rating (3) and advances to the next word.
		/// </summary>
		private void HandleVocabReviewNext()
		{
			var current = GetCurrentVocabulary();
			if (current != null)
			{
				HandleReviewRequested(current.Id, 3);
			}
		}

		/// <summary>
		/// Resolves selected character name, with fallback to the first available scene character.
		/// </summary>
		/// <param name="selectedCharacterName">Character name selected in dropdown.</param>
		/// <returns>Character name for TTS request, or null when unavailable.</returns>
		private string ResolveVocabularyAudioCharacterName(string selectedCharacterName)
		{
			if (!string.IsNullOrWhiteSpace(selectedCharacterName))
			{
				return selectedCharacterName.Trim();
			}

			var availableNames = GetAllCharacterNamesForVocabularyAudio();
			if (availableNames.Count > 0)
			{
				return availableNames[0];
			}

			return null;
		}

		/// <summary>
		/// Loads cached character names used by vocabulary pronunciation dropdown.
		/// </summary>
		/// <returns>Distinct non-empty character names.</returns>
		private List<string> GetAllCharacterNamesForVocabularyAudio()
		{
			var names = SendRequest<List<string>>(PracticeVocabularyRequests.GetAllCharacterNames);
			var result = new List<string>();
			if (names == null || names.Count == 0)
			{
				return result;
			}

			for (var i = 0; i < names.Count; i++)
			{
				var name = names[i];
				if (string.IsNullOrWhiteSpace(name))
				{
					continue;
				}

				result.Add(name.Trim());
			}

			return result;
		}

		/// <summary>
		/// Refreshes character dropdown options in vocabulary popup.
		/// </summary>
		private void RefreshVocabularyCharacterOptions()
		{
			if (_vocabularyPopupView == null)
			{
				return;
			}

			_vocabularyPopupView.SetCharacterOptions(GetAllCharacterNamesForVocabularyAudio());
		}

		/// <summary>
		/// Ensures popup reference and callback wiring are valid.
		/// </summary>
		private void EnsurePopupBinding()
		{
			EnsureAudioSource();

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
			_vocabularyPopupView.SetNextCallback(HandleVocabReviewNext);
			_vocabularyPopupView.SetAudioPlayCallback(HandleVocabularyAudioPlayRequested);
			_vocabularyPopupView.SetGenerateExampleCallback(HandleGenerateVocabExample);
			RefreshVocabularyCharacterOptions();
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

			SetAudioRequestInProgress(false);
			RefreshVocabularyCharacterOptions();
			_vocabularyPopupView.SetNextButtonVisible(false);

			var safeWord = string.IsNullOrWhiteSpace(vocabulary.Korean) ? string.Empty : vocabulary.Korean.Trim();
			_vocabularyPopupView.ShowLoading(safeWord);
			_vocabularyPopupView.ShowResult(vocabulary.Id, safeWord, vocabulary.Pinyin, vocabulary.Vietnamese);
		}

		/// <summary>
		/// Hides popup and keeps queue data untouched.
		/// </summary>
		private void HidePopup()
		{
			SetAudioRequestInProgress(false);

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

		/// <summary>
		/// Ensures the view has an AudioSource for pronunciation playback.
		/// </summary>
		private void EnsureAudioSource()
		{
			if (_characterVoiceAudioSource == null)
			{
				_characterVoiceAudioSource = GetComponent<AudioSource>();
				if (_characterVoiceAudioSource == null)
				{
					_characterVoiceAudioSource = gameObject.AddComponent<AudioSource>();
				}
			}
		}

		/// <summary>
		/// Binds the ignore button click listener.
		/// </summary>
		private void BindIgnoreButton()
		{
			if (_ignoreVocabularyButton != null)
			{
				_ignoreVocabularyButton.onClick.AddListener(HandleIgnoreVocabularyClicked);
			}
		}

		/// <summary>
		/// Unbinds the ignore button click listener.
		/// </summary>
		private void UnbindIgnoreButton()
		{
			if (_ignoreVocabularyButton != null)
			{
				_ignoreVocabularyButton.onClick.RemoveListener(HandleIgnoreVocabularyClicked);
			}
		}

		/// <summary>
		/// Handles ignore button click — sends ignore request for the current vocabulary.
		/// </summary>
		private void HandleIgnoreVocabularyClicked()
		{
			var current = GetCurrentVocabulary();
			if (current == null || string.IsNullOrWhiteSpace(current.Id))
			{
				return;
			}

			SendRequest(PracticeVocabularyRequests.IgnoreVocabulary, new PracticeVocabularyIgnoreRequestPayload
			{
				VocabularyId = current.Id
			});
		}

		/// <summary>
		/// Updates popup state while waiting for vocabulary audio response.
		/// </summary>
		/// <param name="isInProgress">True while waiting for server response.</param>
		private void SetAudioRequestInProgress(bool isInProgress)
		{
			_isAudioRequestInProgress = isInProgress;
			if (_vocabularyPopupView != null)
			{
				_vocabularyPopupView.SetAudioRequestInProgress(isInProgress);
			}
		}
	}
}
