using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.Chat.View
{
	/// <summary>
	/// Manages context input popup UI in chat feature.
	/// </summary>
	public sealed class ChatContextPopupView : MonoBehaviour
	{
		public Action<string> OnSaveContextClicked;

		[SerializeField]
		private TMP_InputField _contextInputField;

		[SerializeField]
		private Button _saveButton;

		[SerializeField]
		private Button _closeButton;

		private void Awake()
		{
			EnsureReferences();
			BindButtons();
		}

		private void OnDestroy()
		{
			if (_saveButton != null)
			{
				_saveButton.onClick.RemoveListener(HandleSaveButtonClicked);
			}

			if (_closeButton != null)
			{
				_closeButton.onClick.RemoveListener(Hide);
			}
		}

		/// <summary>
		/// Shows the context popup and focuses input field.
		/// </summary>
		/// <param name="currentContext">Optional context value to prefill.</param>
		public void Show(string currentContext = null)
		{
			EnsureReferences();
			if (!gameObject.activeSelf)
			{
				gameObject.SetActive(true);
			}

			if (_contextInputField != null)
			{
				_contextInputField.text = string.IsNullOrWhiteSpace(currentContext) ? string.Empty : currentContext.Trim();
				_contextInputField.ActivateInputField();
			}
		}

		/// <summary>
		/// Hides the popup immediately.
		/// </summary>
		public void Hide()
		{
			if (gameObject.activeSelf)
			{
				gameObject.SetActive(false);
			}
		}

		/// <summary>
		/// Hides popup without any transition.
		/// </summary>
		public void HideImmediate()
		{
			Hide();
		}

		private void HandleSaveButtonClicked()
		{
			var context = _contextInputField != null
				? (_contextInputField.text ?? string.Empty).Trim()
				: string.Empty;

			OnSaveContextClicked?.Invoke(context);
			Hide();
		}

		private void BindButtons()
		{
			if (_saveButton != null)
			{
				_saveButton.onClick.RemoveListener(HandleSaveButtonClicked);
				_saveButton.onClick.AddListener(HandleSaveButtonClicked);
			}

			if (_closeButton != null)
			{
				_closeButton.onClick.RemoveListener(Hide);
				_closeButton.onClick.AddListener(Hide);
			}
		}

		private void EnsureReferences()
		{
			if (_contextInputField == null)
			{
				var input = transform.Find("Panel/inputContext");
				if (input != null)
				{
					_contextInputField = input.GetComponent<TMP_InputField>();
				}
			}

			if (_saveButton == null)
			{
				var saveButton = transform.Find("Panel/btnSaveContext");
				if (saveButton != null)
				{
					_saveButton = saveButton.GetComponent<Button>();
				}
			}

			if (_closeButton == null)
			{
				var closeButton = transform.Find("Panel/close");
				if (closeButton != null)
				{
					_closeButton = closeButton.GetComponent<Button>();
				}
			}
		}

		private void OnValidate()
		{
			EnsureReferences();
		}
	}
}
