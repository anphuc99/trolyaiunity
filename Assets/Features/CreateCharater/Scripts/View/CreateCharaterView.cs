using System.Collections.Generic;
using Core.Infrastructure.Views;
using Features.CreateCharater.Events;
using Features.CreateCharater.Infrastructure.Attributes;
using Features.CreateCharater.Model;
using Features.CreateCharater.Requests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Features.CreateCharater.View
{
	/// <summary>
	/// View for CreateCharater.
	/// Handles user input for character creation and personality selection.
	/// </summary>
	public sealed class CreateCharaterView : BaseView
	{
		[Header("Input Fields")]
		[SerializeField] private TMP_InputField _nameInput;
		[SerializeField] private TMP_InputField _ageInput;
		[SerializeField] private TMP_InputField _genderInput;
		[SerializeField] private TMP_InputField _descriptionInput;

		[Header("Personalities")]
		[SerializeField] private RectTransform _personalityContainer;
		[SerializeField] private Button _personalityButtonPrefab;
		[SerializeField] private Color _selectedColor = Color.green;
		[SerializeField] private Color _normalColor = Color.white;

		[Header("Actions")]
		[SerializeField] private Button _submitButton;

		private readonly List<string> _selectedPersonalities = new List<string>();
		private readonly Dictionary<string, Button> _personalityButtons = new Dictionary<string, Button>();

		/// <summary>
		/// Called after this view is enabled and scope is active.
		/// </summary>
		protected override void OnEnabled()
		{
			if (_submitButton != null)
			{
				_submitButton.onClick.AddListener(OnSubmitClicked);
			}

			// Initial fetch of personalities
			SendRequest(CreateCharaterRequests.FetchPersonalities);
		}

		protected override void OnDisabled()
		{
			if (_submitButton != null)
			{
				_submitButton.onClick.RemoveListener(OnSubmitClicked);
			}
		}

		/// <summary>
		/// Handles the event when personalities are loaded from the server.
		/// </summary>
		/// <param name="payload">List of PersonalityData.</param>
		[OnEvent(CreateCharaterEvents.PersonalitiesLoaded)]
		private void OnPersonalitiesLoaded(object payload)
		{
			var personalities = payload as List<PersonalityData>;
			if (personalities == null) return;

			// Clear existing
			foreach (var btn in _personalityButtons.Values)
			{
				Destroy(btn.gameObject);
			}
			_personalityButtons.Clear();
			_selectedPersonalities.Clear();

			// Spawn buttons
			foreach (var data in personalities)
			{
				var btn = Instantiate(_personalityButtonPrefab, _personalityContainer);
				var txt = btn.GetComponentInChildren<TMP_Text>();
				if (txt != null)
				{
					txt.text = data.name;
				}

				var personalityName = data.name;
				btn.onClick.AddListener(() => TogglePersonality(personalityName));
				_personalityButtons.Add(personalityName, btn);
				
				UpdateButtonStyle(personalityName, false);
			}
		}

		private void TogglePersonality(string personalityName)
		{
			if (_selectedPersonalities.Contains(personalityName))
			{
				_selectedPersonalities.Remove(personalityName);
				UpdateButtonStyle(personalityName, false);
			}
			else
			{
				_selectedPersonalities.Add(personalityName);
				UpdateButtonStyle(personalityName, true);
			}
		}

		private void UpdateButtonStyle(string personalityName, bool isSelected)
		{
			if (_personalityButtons.TryGetValue(personalityName, out var btn))
			{
				btn.image.color = isSelected ? _selectedColor : _normalColor;
			}
		}

		private void OnSubmitClicked()
		{
			var payload = new CreateCharacterPayload
			{
				name = _nameInput != null ? _nameInput.text : string.Empty,
				age = _ageInput != null && int.TryParse(_ageInput.text, out var age) ? age : 0,
				gender = _genderInput != null ? _genderInput.text : string.Empty,
				description = _descriptionInput != null ? _descriptionInput.text : string.Empty,
				personality = new List<string>(_selectedPersonalities)
			};

			SendRequest(CreateCharaterRequests.SubmitCharacter, payload);
		}

		[OnEvent(CreateCharaterEvents.CharacterCreationSucceeded)]
		private void OnCreationSucceeded(object payload)
		{
			Debug.Log("[CreateCharaterView] Character created successfully!");
			// Potentially move to another screen or show success feedback
		}

		[OnEvent(CreateCharaterEvents.CharacterCreationFailed)]
		private void OnCreationFailed(object payload)
		{
			Debug.LogError($"[CreateCharaterView] Character creation failed: {payload}");
		}
	}
}
