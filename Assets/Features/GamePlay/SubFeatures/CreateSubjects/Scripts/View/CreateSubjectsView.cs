using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.CreateSubjects.Events;
using Features.GamePlay.SubFeatures.CreateSubjects.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.CreateSubjects.Model;
using Features.GamePlay.SubFeatures.CreateSubjects.Requests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.CreateSubjects.View
{
	/// <summary>
	/// View for CreateSubjects subfeature.
	/// Displays a form for creating a new subject with name and description fields.
	/// </summary>
	public sealed class CreateSubjectsView : BaseView
	{
		[SerializeField]
		private TMP_InputField _inputName;

		[SerializeField]
		private TMP_InputField _inputDescription;

		[SerializeField]
		private Button _submitButton;

		[SerializeField]
		private Button _cancelButton;

		[SerializeField]
		private TextMeshProUGUI _errorText;

		/// <summary>
		/// Wires button click listeners when the view is enabled.
		/// </summary>
		protected override void OnEnabled()
		{
			if (_submitButton != null)
			{
				_submitButton.onClick.RemoveListener(OnSubmitClicked);
				_submitButton.onClick.AddListener(OnSubmitClicked);
			}

			if (_cancelButton != null)
			{
				_cancelButton.onClick.RemoveListener(OnCancelClicked);
				_cancelButton.onClick.AddListener(OnCancelClicked);
			}
		}

		/// <summary>
		/// Removes button click listeners when the view is disabled.
		/// </summary>
		protected override void OnDisabled()
		{
			if (_submitButton != null)
			{
				_submitButton.onClick.RemoveListener(OnSubmitClicked);
			}

			if (_cancelButton != null)
			{
				_cancelButton.onClick.RemoveListener(OnCancelClicked);
			}
		}

		/// <summary>
		/// Shows the CreateSubjects view and resets the form.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(CreateSubjectsEvents.Installed)]
		private void OnInstalled(object payload)
		{
			ResetForm();
			gameObject.SetActive(true);
		}

		/// <summary>
		/// Hides the CreateSubjects view.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(CreateSubjectsEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			gameObject.SetActive(false);
		}

		/// <summary>
		/// Disables submit button while request is in progress.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(CreateSubjectsEvents.CreateStarted)]
		private void OnCreateStarted(object payload)
		{
			SetSubmitInteractable(false);
			SetErrorText(null);
		}

		/// <summary>
		/// Handles successful subject creation.
		/// </summary>
		/// <param name="payload">CreateSubjectResponsePayload.</param>
		[OnEvent(CreateSubjectsEvents.CreateSucceeded)]
		private void OnCreateSucceeded(object payload)
		{
			SetSubmitInteractable(true);
			ResetForm();
		}

		/// <summary>
		/// Handles subject creation failure by showing an error message.
		/// </summary>
		/// <param name="payload">CreateSubjectsErrorPayload.</param>
		[OnEvent(CreateSubjectsEvents.CreateFailed)]
		private void OnCreateFailed(object payload)
		{
			SetSubmitInteractable(true);

			var error = payload as CreateSubjectsErrorPayload;
			if (error != null)
			{
				SetErrorText(error.Message);
				Debug.LogError("[CreateSubjectsView] " + error.Message);
			}
		}

		/// <summary>
		/// Submits the form data to create a new subject.
		/// </summary>
		public void OnSubmitClicked()
		{
			var nameText = _inputName != null ? _inputName.text : null;
			var descText = _inputDescription != null ? _inputDescription.text : null;

			SendRequest(CreateSubjectsRequests.SubmitCreate, new CreateSubjectPayload
			{
				Name = nameText,
				Description = descText,
			});
		}

		/// <summary>
		/// Cancels creation and navigates back.
		/// </summary>
		public void OnCancelClicked()
		{
			SendRequest(CreateSubjectsRequests.Cancel);
		}

		/// <summary>
		/// Resets form fields to empty values.
		/// </summary>
		private void ResetForm()
		{
			if (_inputName != null)
			{
				_inputName.text = string.Empty;
			}

			if (_inputDescription != null)
			{
				_inputDescription.text = string.Empty;
			}

			SetErrorText(null);
			SetSubmitInteractable(true);
		}

		/// <summary>
		/// Sets the submit button interactable state.
		/// </summary>
		/// <param name="interactable">Whether the button should be interactable.</param>
		private void SetSubmitInteractable(bool interactable)
		{
			if (_submitButton != null)
			{
				_submitButton.interactable = interactable;
			}
		}

		/// <summary>
		/// Sets the error text message, or hides it when null/empty.
		/// </summary>
		/// <param name="message">Error message to display, or null to clear.</param>
		private void SetErrorText(string message)
		{
			if (_errorText == null)
			{
				return;
			}

			if (string.IsNullOrWhiteSpace(message))
			{
				_errorText.text = string.Empty;
				_errorText.gameObject.SetActive(false);
			}
			else
			{
				_errorText.text = message;
				_errorText.gameObject.SetActive(true);
			}
		}
	}
}
