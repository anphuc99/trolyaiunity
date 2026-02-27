using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Practice.Events;
using Features.GamePlay.SubFeatures.Practice.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Practice.Model;
using Features.GamePlay.SubFeatures.Practice.Requests;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.Practice.View
{
	/// <summary>
	/// View for Practice.
	/// </summary>
	public sealed class PracticeView : BaseView
	{
		private const string MaskedText = "****";
		private const string RevealText = "Hiện chữ";
		private const string ContextText = "Hiện bối cảnh";
		private const string AnswerText = "Hiện đáp án";
		private const string EmptyTranslationText = "Chưa có bản dịch tiếng Việt";

		[SerializeField]
		private TMP_Text _koreanText;

		[SerializeField]
		private TMP_Text _contextBeforeText;

		[SerializeField]
		private TMP_Text _contextAfterText;

		[SerializeField]
		private TMP_Text _revealButtonText;

		[SerializeField]
		private TMP_Text _tabTitleText;

		[SerializeField]
		private Button _revealButton;

		[SerializeField]
		private GameObject _contextBeforeContainer;

		[SerializeField]
		private GameObject _contextAfterContainer;

		[SerializeField]
		private GameObject _ratingContainer;

		[SerializeField]
		private Button _rateAgainButton;

		[SerializeField]
		private Button _rateHardButton;

		[SerializeField]
		private Button _rateGoodButton;

		[SerializeField]
		private Button _rateEasyButton;

		[SerializeField]
		private Button _tabReviewButton;

		[SerializeField]
		private Button _tabDifficultButton;

		[SerializeField]
		private Button _tabStarredButton;

		[SerializeField]
		private Button _tabLearnButton;

		private PracticeTabType _currentTab = PracticeTabType.Review;
		private PracticePromptItemPayload _currentItem;
		private bool _isKoreanRevealed;
		private bool _isContextRevealed;
		private bool _isAnswerRevealed;

		// Local counting state
		private List<PracticePromptItemPayload> _pendingItems = new List<PracticePromptItemPayload>();
		private int _currentItemIndex;
		private int _localLearnedCount;
		private int _totalItemCount;
		private string _currentTabLabel;

		/// <summary>
		/// Shows this subfeature view when its controller is installed.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(PracticeEvents.Installed)]
		private void OnInstalled(object payload)
		{
			gameObject.SetActive(true);
			EnsureDependencies();
			LoadTab(PracticeTabType.Review);
		}

		/// <summary>
		/// Hides this subfeature view when its controller is uninstalled.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(PracticeEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			ClearViewState();
			gameObject.SetActive(false);
		}

		protected override void OnEnabled()
		{
			EnsureDependencies();
		}

		private void EnsureDependencies()
		{
			if (_koreanText == null)
			{
				_koreanText = FindChildByName(transform, "textkr")?.GetComponentInChildren<TMP_Text>();
			}

			if (_contextBeforeContainer == null)
			{
				_contextBeforeContainer = FindChildByName(transform, "5 câu trước")?.gameObject;
			}

			if (_contextAfterContainer == null)
			{
				_contextAfterContainer = FindChildByName(transform, "5 câu sau")?.gameObject;
			}

			if (_contextBeforeText == null && _contextBeforeContainer != null)
			{
				_contextBeforeText = _contextBeforeContainer.GetComponentInChildren<TMP_Text>();
			}

			if (_contextAfterText == null && _contextAfterContainer != null)
			{
				_contextAfterText = _contextAfterContainer.GetComponentInChildren<TMP_Text>();
			}

			if (_revealButton == null)
			{
				_revealButton = FindChildByName(transform, "btn hiện chữ")?.GetComponent<Button>();
			}

			if (_revealButtonText == null && _revealButton != null)
			{
				_revealButtonText = _revealButton.GetComponentInChildren<TMP_Text>();
			}

			if (_tabTitleText == null)
			{
				var titleRoot = FindChildByName(transform, "Icon");
				_tabTitleText = titleRoot != null ? titleRoot.GetComponentInChildren<TMP_Text>() : null;
			}

			if (_ratingContainer == null)
			{
				_ratingContainer = FindChildByName(transform, "btn đánh giá")?.gameObject;
			}

			if (_rateAgainButton == null)
			{
				_rateAgainButton = FindChildByName(transform, "Lại")?.GetComponent<Button>();
			}

			if (_rateHardButton == null)
			{
				_rateHardButton = FindChildByName(transform, "Khó")?.GetComponent<Button>();
			}

			if (_rateGoodButton == null)
			{
				_rateGoodButton = FindChildByName(transform, "Trung bình")?.GetComponent<Button>();
			}

			if (_rateEasyButton == null)
			{
				_rateEasyButton = FindChildByName(transform, "Dễ")?.GetComponent<Button>();
			}

			if (_tabReviewButton == null)
			{
				_tabReviewButton = FindChildByName(transform, "Ôn tập")?.GetComponent<Button>();
			}

			if (_tabDifficultButton == null)
			{
				_tabDifficultButton = FindChildByName(transform, "Từ khó")?.GetComponent<Button>();
			}

			if (_tabStarredButton == null)
			{
				_tabStarredButton = FindChildByName(transform, "Từ sao")?.GetComponent<Button>();
			}

			if (_tabLearnButton == null)
			{
				_tabLearnButton = FindChildByName(transform, "Học")?.GetComponent<Button>();
			}

			BindButtonEvents();
		}

		private void BindButtonEvents()
		{
			BindButton(_revealButton, HandleRevealClicked);
			BindButton(_rateAgainButton, () => HandleRateClicked(1));
			BindButton(_rateHardButton, () => HandleRateClicked(2));
			BindButton(_rateGoodButton, () => HandleRateClicked(3));
			BindButton(_rateEasyButton, () => HandleRateClicked(4));
			BindButton(_tabReviewButton, () => LoadTab(PracticeTabType.Review));
			BindButton(_tabDifficultButton, () => LoadTab(PracticeTabType.Difficult));
			BindButton(_tabStarredButton, () => LoadTab(PracticeTabType.Starred));
			BindButton(_tabLearnButton, () => LoadTab(PracticeTabType.Learn));
		}

		private static void BindButton(Button button, Action handler)
		{
			if (button == null || handler == null)
			{
				return;
			}

			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(() => handler());
		}

		private void LoadTab(PracticeTabType tab)
		{
			_currentTab = tab;
			SendRequest(PracticeRequests.LoadTab, new PracticeTabRequestPayload
			{
				Tab = tab
			});
		}

		[OnEvent(PracticeEvents.TabLoaded)]
		private void OnTabLoaded(object payload)
		{
			if (payload is not PracticeTabResponsePayload response || response.Items == null)
			{
				return;
			}

			// Initialize local counting state
			_pendingItems = new List<PracticePromptItemPayload>(response.Items);
			_currentItemIndex = 0;
			_localLearnedCount = 0;
			_totalItemCount = response.Items.Count;
			_currentTabLabel = response.Summary?.TabLabel ?? GetTabLabel(response.Tab);

			// Update header with 0/total
			UpdateLocalCountHeader();

			if (response.Items.Count == 0)
			{
				ShowEmptyState();
				return;
			}

			_currentItem = _pendingItems[0];
			ResetRevealState();
		}

		[OnEvent(PracticeEvents.ContextLoaded)]
		private void OnContextLoaded(object payload)
		{
			if (payload is not PracticeTranslationContextResponsePayload response)
			{
				return;
			}

			if (_contextBeforeContainer != null)
			{
				_contextBeforeContainer.SetActive(true);
			}

			if (_contextAfterContainer != null)
			{
				_contextAfterContainer.SetActive(true);
			}

			if (_contextBeforeText != null)
			{
				_contextBeforeText.text = BuildContextText("5 câu trước", response.Before);
			}

			if (_contextAfterText != null)
			{
				_contextAfterText.text = BuildContextText("5 câu sau", response.After);
			}

			_isContextRevealed = true;
			UpdateRevealButtonText(AnswerText);
		}

		[OnEvent(PracticeEvents.ReviewSubmitted)]
		private void OnReviewSubmitted(object payload)
		{
			// Increment local count
			_localLearnedCount += 1;
			_currentItemIndex += 1;

			// Update header with new count
			UpdateLocalCountHeader();

			// Check if we have more pending items locally
			if (_currentItemIndex < _pendingItems.Count)
			{
				_currentItem = _pendingItems[_currentItemIndex];
				ResetRevealState();
				return;
			}

			// All local items done - reload from server to check for more
			LoadTab(_currentTab);
		}

		[OnEvent(PracticeEvents.RequestFailed)]
		private void OnRequestFailed(object payload)
		{
			var error = payload as PracticeErrorPayload;
			Debug.LogWarning("[PracticeView] Request failed: " + (error?.Message ?? "Unknown error"), this);
		}

		private void HandleRevealClicked()
		{
			if (_currentItem == null)
			{
				return;
			}

			if (!_isKoreanRevealed)
			{
				ShowKoreanText();
				_isKoreanRevealed = true;
				UpdateRevealButtonText(ContextText);
				return;
			}

			if (!_isContextRevealed)
			{
				SendRequest(PracticeRequests.LoadContext, new PracticeContextRequestPayload
				{
					MessageId = _currentItem.MessageId
				});
				return;
			}

			if (!_isAnswerRevealed)
			{
				ShowAnswer();
			}
		}

		private void HandleRateClicked(int rating)
		{
			if (_currentItem == null)
			{
				return;
			}

			SetRatingInteractable(false);

			SendRequest(PracticeRequests.SubmitReview, new PracticeReviewRequestPayload
			{
				Rating = rating,
				CardId = _currentItem.CardId,
				MessageId = _currentItem.MessageId
			});
		}

		private void ShowKoreanText()
		{
			if (_koreanText == null)
			{
				return;
			}

			_koreanText.text = string.IsNullOrWhiteSpace(_currentItem?.Content) ? string.Empty : _currentItem.Content;
		}

		private void ShowAnswer()
		{
			if (_koreanText != null)
			{
				var translation = string.IsNullOrWhiteSpace(_currentItem?.Translation)
					? EmptyTranslationText
					: _currentItem.Translation;
				_koreanText.text = (_currentItem?.Content ?? string.Empty) + " - " + translation;
			}

			_isAnswerRevealed = true;
			UpdateRevealButtonText(AnswerText);
			SetRatingContainerVisible(true);
			if (_revealButton != null)
			{
				_revealButton.interactable = false;
			}
		}

		private void ResetRevealState()
		{
			_isKoreanRevealed = false;
			_isContextRevealed = false;
			_isAnswerRevealed = false;

			if (_koreanText != null)
			{
				_koreanText.text = MaskedText;
			}

			if (_contextBeforeText != null)
			{
				_contextBeforeText.text = string.Empty;
			}

			if (_contextAfterText != null)
			{
				_contextAfterText.text = string.Empty;
			}

			if (_contextBeforeContainer != null)
			{
				_contextBeforeContainer.SetActive(false);
			}

			if (_contextAfterContainer != null)
			{
				_contextAfterContainer.SetActive(false);
			}

			UpdateRevealButtonText(RevealText);
			if (_revealButton != null)
			{
				_revealButton.interactable = true;
			}

			SetRatingContainerVisible(false);
			SetRatingInteractable(true);
		}

		private void ShowEmptyState()
		{
			_currentItem = null;
			if (_koreanText != null)
			{
				_koreanText.text = "Không có dữ liệu";
			}

			UpdateRevealButtonText(RevealText);
			if (_revealButton != null)
			{
				_revealButton.interactable = false;
			}

			SetRatingContainerVisible(false);
			if (_contextBeforeContainer != null)
			{
				_contextBeforeContainer.SetActive(false);
			}

			if (_contextAfterContainer != null)
			{
				_contextAfterContainer.SetActive(false);
			}
		}

		private void ClearViewState()
		{
			_currentItem = null;
			_pendingItems.Clear();
			_currentItemIndex = 0;
			_localLearnedCount = 0;
			_totalItemCount = 0;
			_currentTabLabel = string.Empty;

			if (_koreanText != null)
			{
				_koreanText.text = string.Empty;
			}

			if (_contextBeforeText != null)
			{
				_contextBeforeText.text = string.Empty;
			}

			if (_contextAfterText != null)
			{
				_contextAfterText.text = string.Empty;
			}

			SetRatingContainerVisible(false);
		}

		private void UpdateRevealButtonText(string text)
		{
			if (_revealButtonText != null)
			{
				_revealButtonText.text = text;
			}
		}

		/// <summary>
		/// Updates the header title with local learned count and total.
		/// </summary>
		private void UpdateLocalCountHeader()
		{
			if (_tabTitleText == null)
			{
				return;
			}

			var label = string.IsNullOrWhiteSpace(_currentTabLabel) ? GetTabLabel(_currentTab) : _currentTabLabel;
			_tabTitleText.text = label + " (" + _localLearnedCount + "/" + _totalItemCount + ")";
		}

		private void SetRatingContainerVisible(bool isVisible)
		{
			if (_ratingContainer != null)
			{
				_ratingContainer.SetActive(isVisible);
			}
		}

		private void SetRatingInteractable(bool isInteractable)
		{
			SetButtonInteractable(_rateAgainButton, isInteractable);
			SetButtonInteractable(_rateHardButton, isInteractable);
			SetButtonInteractable(_rateGoodButton, isInteractable);
			SetButtonInteractable(_rateEasyButton, isInteractable);
		}

		private static void SetButtonInteractable(Button button, bool isInteractable)
		{
			if (button != null)
			{
				button.interactable = isInteractable;
			}
		}

		private static string BuildContextText(string title, List<PracticeContextMessagePayload> messages)
		{
			var lines = new List<string> { title + ":" };
			if (messages != null)
			{
				for (var i = 0; i < messages.Count; i += 1)
				{
					var message = messages[i];
					if (message == null)
					{
						continue;
					}

					var name = string.IsNullOrWhiteSpace(message.CharacterName) ? "Unknown" : message.CharacterName.Trim();
					var text = string.IsNullOrWhiteSpace(message.Text) ? string.Empty : message.Text.Trim();
					lines.Add((i + 1) + ". <b>" + name + "</b>: " + text);
				}
			}

			return string.Join("\n", lines);
		}

		/// <summary>
		/// Gets a display label for the tab when summary data is missing.
		/// </summary>
		/// <param name="tab">Target tab.</param>
		/// <returns>Localized tab label.</returns>
		private static string GetTabLabel(PracticeTabType tab)
		{
			return tab switch
			{
				PracticeTabType.Review => "Ôn tập",
				PracticeTabType.Difficult => "Từ khó",
				PracticeTabType.Starred => "Từ sao",
				PracticeTabType.Learn => "Học",
				_ => "Luyện tập"
			};
		}

		private static Transform FindChildByName(Transform root, string targetName)
		{
			if (root == null || string.IsNullOrWhiteSpace(targetName))
			{
				return null;
			}

			for (var i = 0; i < root.childCount; i += 1)
			{
				var child = root.GetChild(i);
				if (child == null)
				{
					continue;
				}

				if (string.Equals(child.name, targetName, StringComparison.Ordinal))
				{
					return child;
				}

				var nested = FindChildByName(child, targetName);
				if (nested != null)
				{
					return nested;
				}
			}

			return null;
		}
	}
}
