using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup view for displaying vocabulary details in chat.
/// Shown when user clicks a marked vocabulary word in a chat bubble,
/// or when reviewing used vocabulary after a conversation ends.
/// </summary>
namespace Share.Components
{
    public class SharedVocabularyPopupView : MonoBehaviour
    {
        [Header("Từ và nghĩa")]
        [SerializeField] private TMP_Text _vocabText;
        [SerializeField] private TMP_Text _pinyinText;
        [SerializeField] private TMP_Text _meaningText;
        [SerializeField] private Button _showMeaningButton;

        [Header("Button")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _nextButton;

        [SerializeField] private GameObject _loadingIndicator;
        [SerializeField] private GameObject _contentGroup;

        [Header("Âm thanh")]
        [SerializeField] private Button _playAudioButton;
        [SerializeField] private TMP_Dropdown _characterDropdown;

        /// <summary>
        /// Stores the current vocabulary ID for external reference.
        /// </summary>
        private string _currentVocabularyId;

        /// <summary>
        /// Callback invoked whenever popup is closed.
        /// </summary>
        private System.Action _onClosed;

        /// <summary>
        /// Callback invoked when the Next button is clicked.
        /// </summary>
        private System.Action _onNextRequested;

        /// <summary>
        /// Callback for audio playback request: (word, characterName).
        /// </summary>
        private System.Action<string, string> _onPlayAudioRequested;

        /// <summary>
        /// True while lookup data is loading.
        /// </summary>
        private bool _isLoading;

        /// <summary>
        /// True while waiting for vocabulary audio response from server.
        /// </summary>
        private bool _isAudioRequestInProgress;

        private void Awake()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Hide);
            }

            if (_nextButton != null)
            {
                _nextButton.onClick.AddListener(HandleNext);
            }

            if (_playAudioButton != null)
            {
                _playAudioButton.onClick.AddListener(HandlePlayAudio);
            }

            if (_showMeaningButton != null)
            {
                _showMeaningButton.onClick.AddListener(ShowMeaning);
            }

            SetNextButtonVisible(false);
            UpdateAudioControlsState();

            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(Hide);
            }

            if (_nextButton != null)
            {
                _nextButton.onClick.RemoveListener(HandleNext);
            }

            if (_playAudioButton != null)
            {
                _playAudioButton.onClick.RemoveListener(HandlePlayAudio);
            }

            if (_showMeaningButton != null)
            {
                _showMeaningButton.onClick.RemoveListener(ShowMeaning);
            }
        }

        /// <summary>
        /// No-op kept for backward compatibility. Rating buttons have been removed.
        /// </summary>
        /// <param name="onReviewRequested">Unused.</param>
        public void SetReviewCallback(System.Action<string, int> onReviewRequested)
        {
        }

        /// <summary>
        /// Registers callback fired when popup closes.
        /// </summary>
        /// <param name="onClosed">Close callback.</param>
        public void SetClosedCallback(System.Action onClosed)
        {
            _onClosed = onClosed;
        }

        /// <summary>
        /// Registers callback fired when the Next button is clicked.
        /// </summary>
        /// <param name="onNextRequested">Next callback.</param>
        public void SetNextCallback(System.Action onNextRequested)
        {
            _onNextRequested = onNextRequested;
        }

        /// <summary>
        /// Registers callback fired when play-audio button is clicked.
        /// </summary>
        /// <param name="onPlayAudioRequested">Callback signature: (word, characterName).</param>
        public void SetAudioPlayCallback(System.Action<string, string> onPlayAudioRequested)
        {
            _onPlayAudioRequested = onPlayAudioRequested;
            UpdateAudioControlsState();
        }

        /// <summary>
        /// Updates vocab-audio request progress state.
        /// While true, speaker button is hidden until server response arrives.
        /// </summary>
        /// <param name="isInProgress">True when waiting for server audio generation.</param>
        public void SetAudioRequestInProgress(bool isInProgress)
        {
            _isAudioRequestInProgress = isInProgress;
            UpdateAudioControlsState();
        }

        /// <summary>
        /// Replaces character dropdown options used for pronunciation playback.
        /// </summary>
        /// <param name="characterNames">Character display names.</param>
        public void SetCharacterOptions(IReadOnlyList<string> characterNames)
        {
            if (_characterDropdown == null)
            {
                return;
            }

            _characterDropdown.ClearOptions();

            if (characterNames == null || characterNames.Count == 0)
            {
                _characterDropdown.RefreshShownValue();
                UpdateAudioControlsState();
                return;
            }

            var options = new List<string>();
            for (var i = 0; i < characterNames.Count; i++)
            {
                var name = characterNames[i];
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                options.Add(name.Trim());
            }

            if (options.Count > 0)
            {
                _characterDropdown.AddOptions(options);
                _characterDropdown.value = 0;
            }

            _characterDropdown.RefreshShownValue();
            UpdateAudioControlsState();
        }

        /// <summary>
        /// Shows the popup in a loading state while waiting for server lookup.
        /// </summary>
        /// <param name="word">Chinese word being looked up.</param>
        public void ShowLoading(string word)
        {
            _currentVocabularyId = null;
            _isAudioRequestInProgress = false;
            gameObject.SetActive(true);

            if (_vocabText != null)
            {
                _vocabText.text = word ?? string.Empty;
            }

            if (_pinyinText != null)
            {
                _pinyinText.text = string.Empty;
            }

            if (_meaningText != null)
            {
                _meaningText.text = string.Empty;
            }

            SetLoadingState(true);
            UpdateAudioControlsState();
        }

        /// <summary>
        /// Shows the result once the server lookup completes.
        /// </summary>
        /// <param name="id">Vocabulary id.</param>
        /// <param name="word">Vocabulary word.</param>
        /// <param name="pinyin">Pinyin value.</param>
        /// <param name="meaning">Meaning/translation text.</param>
        public void ShowResult(string id, string word, string pinyin, string meaning)
        {
            _currentVocabularyId = id;
            _isAudioRequestInProgress = false;

            if (_vocabText != null)
            {
                _vocabText.text = word ?? string.Empty;
            }

            if (_pinyinText != null)
            {
                _pinyinText.text = pinyin ?? string.Empty;
            }

            if (_meaningText != null)
            {
                _meaningText.text = meaning ?? string.Empty;
            }
            
            _meaningText.gameObject.SetActive(false);
            SetLoadingState(false);
            UpdateAudioControlsState();
        }

        /// <summary>
        /// No-op kept for backward compatibility. Rating buttons have been removed.
        /// </summary>
        /// <param name="visible">Unused.</param>
        public void SetRatingButtonsVisible(bool visible)
        {
        }

        /// <summary>
        /// Shows or hides the Next button.
        /// </summary>
        /// <param name="visible">Whether the Next button should be visible.</param>
        public void SetNextButtonVisible(bool visible)
        {
            if (_nextButton != null)
            {
                _nextButton.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// Hides the popup.
        /// </summary>
        public void Hide()
        {
            var wasVisible = gameObject.activeSelf;
            _currentVocabularyId = null;
            _isAudioRequestInProgress = false;
            gameObject.SetActive(false);
            UpdateAudioControlsState();

            if (wasVisible)
            {
                _onClosed?.Invoke();
            }
        }

        /// <summary>
        /// Handles "Next" button click.
        /// </summary>
        private void HandleNext()
        {
            _onNextRequested?.Invoke();
        }

        /// <summary>
        /// Handles speaker button click by forwarding current word and selected character.
        /// </summary>
        private void HandlePlayAudio()
        {
            if (_onPlayAudioRequested == null)
            {
                return;
            }

            var word = _vocabText != null ? _vocabText.text : null;
            if (string.IsNullOrWhiteSpace(word))
            {
                return;
            }

            _onPlayAudioRequested.Invoke(word.Trim(), GetSelectedCharacterName());
        }

        /// <summary>
        /// Returns currently selected character name in dropdown.
        /// </summary>
        /// <returns>Selected character name or null when unavailable.</returns>
        private string GetSelectedCharacterName()
        {
            if (_characterDropdown == null || _characterDropdown.options == null || _characterDropdown.options.Count == 0)
            {
                return null;
            }

            var optionIndex = _characterDropdown.value;
            if (optionIndex < 0 || optionIndex >= _characterDropdown.options.Count)
            {
                optionIndex = 0;
            }

            var optionData = _characterDropdown.options[optionIndex];
            return optionData != null ? optionData.text : null;
        }

        /// <summary>
        /// Toggles the loading indicator and content group visibility.
        /// </summary>
        /// <param name="isLoading">Whether the popup is in loading state.</param>
        private void SetLoadingState(bool isLoading)
        {
            _isLoading = isLoading;

            if (_loadingIndicator != null)
            {
                _loadingIndicator.SetActive(isLoading);
            }

            if (_contentGroup != null)
            {
                _contentGroup.SetActive(!isLoading);
            }

            UpdateAudioControlsState();
        }

        /// <summary>
        /// Enables or disables the Next button.
        /// </summary>
        /// <param name="interactable">Whether button should be interactable.</param>
        private void SetNextButtonInteractable(bool interactable)
        {
            if (_nextButton != null)
            {
                _nextButton.interactable = interactable;
            }
        }

        /// <summary>
        /// Syncs speaker and dropdown interactable state with current popup data.
        /// </summary>
        private void UpdateAudioControlsState()
        {
            var hasWord = _vocabText != null && !string.IsNullOrWhiteSpace(_vocabText.text);
            var hasCallback = _onPlayAudioRequested != null;
            var hasCharacterSelection = _characterDropdown == null
                || (_characterDropdown.options != null && _characterDropdown.options.Count > 0);
            var canPlay = gameObject.activeSelf && !_isLoading && !_isAudioRequestInProgress && hasWord && hasCallback && hasCharacterSelection;

            if (_characterDropdown != null)
            {
                _characterDropdown.interactable = gameObject.activeSelf && !_isLoading && !_isAudioRequestInProgress && hasCharacterSelection;
            }

            if (_playAudioButton != null)
            {
                _playAudioButton.gameObject.SetActive(!_isAudioRequestInProgress);
                _playAudioButton.interactable = canPlay;
            }
        }

        private void ShowMeaning()
        {
            if (_meaningText != null)
            {
                _meaningText.gameObject.SetActive(true);
            }
        }
    }
}
