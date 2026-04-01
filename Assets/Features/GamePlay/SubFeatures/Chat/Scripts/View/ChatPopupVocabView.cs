using Features.GamePlay.SubFeatures.Chat.Model;
using Features.GamePlay.SubFeatures.Chat.Requests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup view for displaying vocabulary details and collecting review ratings in chat.
/// Shown when user clicks a marked vocabulary word in a chat bubble.
/// </summary>
public class ChatPopupVocabView : MonoBehaviour
{
    [Header("Từ và nghĩa")]
    [SerializeField] private TMP_Text _vocabText;
    [SerializeField] private TMP_Text _pinyinText;
    [SerializeField] private TMP_Text _meaningText;

    [Header("Đánh giá")]
    [SerializeField] private Button _ratingAgainButton;
    [SerializeField] private Button _ratingHardButton;
    [SerializeField] private Button _ratingGoodButton;
    [SerializeField] private Button _ratingEasyButton;

    [Header("Button")]
    [SerializeField] private Button _closeButton;

    [SerializeField] private GameObject _loadingIndicator;
    [SerializeField] private GameObject _contentGroup;

    /// <summary>
    /// Stores the current vocabulary ID for review submission.
    /// </summary>
    private string _currentVocabularyId;

    /// <summary>
    /// Reference to the BaseView for sending requests.
    /// Set by ChatView when initializing.
    /// </summary>
    private System.Action<ChatVocabReviewRequestPayload> _onReviewRequested;

    /// <summary>
    /// Callback invoked whenever popup is closed.
    /// </summary>
    private System.Action _onClosed;

    private void Awake()
    {
        if (_ratingAgainButton != null)
        {
            _ratingAgainButton.onClick.AddListener(HandleRatingAgain);
        }

        if (_ratingHardButton != null)
        {
            _ratingHardButton.onClick.AddListener(HandleRatingHard);
        }

        if (_ratingGoodButton != null)
        {
            _ratingGoodButton.onClick.AddListener(HandleRatingGood);
        }

        if (_ratingEasyButton != null)
        {
            _ratingEasyButton.onClick.AddListener(HandleRatingEasy);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(Hide);
        }

        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_ratingAgainButton != null)
        {
            _ratingAgainButton.onClick.RemoveListener(HandleRatingAgain);
        }

        if (_ratingHardButton != null)
        {
            _ratingHardButton.onClick.RemoveListener(HandleRatingHard);
        }

        if (_ratingGoodButton != null)
        {
            _ratingGoodButton.onClick.RemoveListener(HandleRatingGood);
        }

        if (_ratingEasyButton != null)
        {
            _ratingEasyButton.onClick.RemoveListener(HandleRatingEasy);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Hide);
        }
    }

    /// <summary>
    /// Registers the callback for review rating submission.
    /// </summary>
    /// <param name="onReviewRequested">Callback to invoke with review payload.</param>
    public void SetReviewCallback(System.Action<ChatVocabReviewRequestPayload> onReviewRequested)
    {
        _onReviewRequested = onReviewRequested;
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
    /// Shows the popup in a loading state while waiting for server lookup.
    /// </summary>
    /// <param name="word">Chinese word being looked up.</param>
    public void ShowLoading(string word)
    {
        _currentVocabularyId = null;
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
        SetRatingButtonsInteractable(false);
    }

    /// <summary>
    /// Shows the result once the server lookup completes.
    /// </summary>
    /// <param name="result">Vocabulary lookup result payload.</param>
    public void ShowResult(ChatVocabLookupResultPayload result)
    {
        if (result == null)
        {
            return;
        }

        _currentVocabularyId = result.Id;

        if (_vocabText != null)
        {
            _vocabText.text = result.Word ?? string.Empty;
        }

        if (_pinyinText != null)
        {
            _pinyinText.text = result.Pinyin ?? string.Empty;
        }

        if (_meaningText != null)
        {
            _meaningText.text = result.Vietnamese ?? string.Empty;
        }

        SetLoadingState(false);
        SetRatingButtonsInteractable(true);
    }

    /// <summary>
    /// Hides the popup.
    /// </summary>
    public void Hide()
    {
        var wasVisible = gameObject.activeSelf;
        _currentVocabularyId = null;
        gameObject.SetActive(false);

        if (wasVisible)
        {
            _onClosed?.Invoke();
        }
    }

    /// <summary>
    /// Handles "Again" rating button click (FSRS rating 1 = Again/Hard).
    /// </summary>
    private void HandleRatingAgain()
    {
        SubmitRating(1);
    }

    /// <summary>
    /// Handles "Hard" rating button click (FSRS rating 2 = Hard).
    /// </summary>
    private void HandleRatingHard()
    {
        SubmitRating(2);
    }

    /// <summary>
    /// Handles "Good" rating button click (FSRS rating 3 = Good).
    /// </summary>
    private void HandleRatingGood()
    {
        SubmitRating(3);
    }

    /// <summary>
    /// Handles "Easy" rating button click (FSRS rating 4 = Easy).
    /// </summary>
    private void HandleRatingEasy()
    {
        SubmitRating(4);
    }

    /// <summary>
    /// Submits the review rating and hides the popup.
    /// </summary>
    /// <param name="rating">FSRS rating value.</param>
    private void SubmitRating(int rating)
    {
        if (string.IsNullOrWhiteSpace(_currentVocabularyId))
        {
            return;
        }

        _onReviewRequested?.Invoke(new ChatVocabReviewRequestPayload
        {
            VocabularyId = _currentVocabularyId,
            Rating = rating
        });

        SetRatingButtonsInteractable(false);
    }

    /// <summary>
    /// Toggles the loading indicator and content group visibility.
    /// </summary>
    /// <param name="isLoading">Whether the popup is in loading state.</param>
    private void SetLoadingState(bool isLoading)
    {
        if (_loadingIndicator != null)
        {
            _loadingIndicator.SetActive(isLoading);
        }

        if (_contentGroup != null)
        {
            _contentGroup.SetActive(!isLoading);
        }
    }

    /// <summary>
    /// Enables or disables the rating buttons.
    /// </summary>
    /// <param name="interactable">Whether buttons should be interactable.</param>
    private void SetRatingButtonsInteractable(bool interactable)
    {
        if (_ratingAgainButton != null)
        {
            _ratingAgainButton.interactable = interactable;
        }

        if (_ratingHardButton != null)
        {
            _ratingHardButton.interactable = interactable;
        }

        if (_ratingGoodButton != null)
        {
            _ratingGoodButton.interactable = interactable;
        }

        if (_ratingEasyButton != null)
        {
            _ratingEasyButton.interactable = interactable;
        }
    }
}
